using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CSVParse;

/// <summary>
/// Provides static methods to parse/write CSV files.
/// </summary>
public static class CSVParser
{
    /// <summary>
    /// Parses a CSV file into an IEnumerable of rows.
    /// </summary>
    /// <typeparam name="T">The row data structure to deserialize into.</typeparam>
    /// <param name="stream">The binary stream to read from.</param>
    /// <param name="options">The CSV parser options.</param>
    /// <param name="leaveOpen">Whether the stream should be left open after parsing has finished.</param>
    /// <returns>An enumerable of rows, parsed as they are iterated through.</returns>
    public static IEnumerable<T> Parse<T>(Stream stream, CSVSerializerOptions? options = null, bool leaveOpen = true) where T : new()
    {
        return new CSVParser<T>(options).Parse(stream, leaveOpen);
    }
}

/// <summary>
/// Defines the options used by the CSV parser.
/// </summary>
public class CSVSerializerOptions
{
    /// <summary>
    /// Whether the target class/struct's fields should be treated as CSV fields.
    /// </summary>
    public bool IncludeFields { get; init; } = false;
    /// <summary>
    /// Whether the target class/struct's properties should be treated as CSV fields.
    /// </summary>
    public bool IncludeProperties { get; init; } = true;
    /// <summary>
    /// Whether the target class/struct's private members should be treated as CSV fields.
    /// </summary>
    public bool IncludePrivate { get; init; } = false;
    /// <summary>
    /// Whether the parser should parse and unescape quotation marks if encountered in the CSV file.
    /// </summary>
    public bool HandleSpeechMarks { get; init; } = false;
    /// <summary>
    /// The separator character between fields in the CSV.
    /// </summary>
    public char Separator { get; init; } = ',';
    /// <summary>
    /// The absolute maximum length of a line in the CSV file. If lines exceed this length, the behaviour is undefined.
    /// </summary>
    public int MaximumLineSize { get; init; } = 2048;
    /// <summary>
    /// The CSV parser's behaviour when reading a CSV header row.
    /// </summary>
    public CSVHeaderMode HeaderMode { get; init; } = CSVHeaderMode.Parse;
    /// <summary>
    /// The default text encoding to use when decoding the file. If left as <c>null</c>, this will be detected automatically.
    /// </summary>
    public Encoding? DefaultEncoding { get; init; } = null;
    /// <summary>
    /// Whether to use multithreading to parse the CSV file.
    /// </summary>
    public bool Multithreaded { get; init; } = false;
    /// <summary>
    /// Whether to use the FastFloat algorithm provided by https://github.com/CarlVerret/csFastFloat to parse floating point numbers.
    /// </summary>
    public bool UseFastFloat { get; init; } = true;

    public static readonly CSVSerializerOptions Default = new();

    public CSVSerializerOptions() { }

    public CSVSerializerOptions(CSVSerializerOptions other)
    {
        IncludeFields = other.IncludeFields;
        IncludeProperties = other.IncludeProperties;
        IncludePrivate = other.IncludePrivate;
        HandleSpeechMarks = other.HandleSpeechMarks;
        Separator = other.Separator;
        MaximumLineSize = other.MaximumLineSize;
        HeaderMode = other.HeaderMode;
        DefaultEncoding = other.DefaultEncoding;
        Multithreaded = other.Multithreaded;
        UseFastFloat = other.UseFastFloat;
    }
}

/// <summary>
/// Parses CSV files into C# data structures.
/// </summary>
/// <typeparam name="T">The type of the data structure representing a single row of the CSV.</typeparam>
[RequiresUnreferencedCode("")]
public class CSVParser<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties)] T> where T : new()
{
    private readonly byte[] byteBuffer;
    private readonly char[] line;
    private readonly char[] lineUnescaped;
    private readonly ReflectionData<T>?[] typeInfo;
    private readonly CSVSerializerOptions options;
    private char[] charBuffer;
    private Encoding encoding;
    private Decoder decoder;
    private int position = 0;
    private int charBuffLength = 0;
    private bool detectedEncoding = false;

    private const int defaultReadSize = 4096;
    //private const int defaultReadSize = 10;

    /// <summary>
    /// Constructs a new CSV Parser with the given options.
    /// </summary>
    /// <param name="options">The options to initialise this parser instance with.</param>
    public CSVParser(CSVSerializerOptions? options = null)
    {
        this.options = options ?? CSVSerializerOptions.Default;
        byteBuffer = new byte[defaultReadSize];
        line = new char[this.options.MaximumLineSize];
        lineUnescaped = new char[this.options.MaximumLineSize];
        Init();
        typeInfo = BuildReflectionCache();
    }

    [MemberNotNull(nameof(encoding))]
    [MemberNotNull(nameof(charBuffer))]
    [MemberNotNull(nameof(decoder))]
    private void Init(Encoding? encoding = null)
    {
        this.encoding = encoding ?? Encoding.Default;
        detectedEncoding = encoding != null;
        decoder = this.encoding.GetDecoder();

        position = 0;
        charBuffLength = 0;

        charBuffer = new char[this.encoding.GetMaxCharCount(byteBuffer.Length)];
    }

    /// <summary>
    /// Resets the internal state of the parser, and if enabled, reads the header row of given CSV stream.
    /// </summary>
    /// <param name="stream">A binary stream containing the CSV file to be parsed.</param>
    /// <returns>A data structure storing the contents and metadata from the header row.</returns>
    public HeaderData<T> Initialise(Stream stream)
    {
        Init(options.DefaultEncoding);

        var fields = typeInfo;
        int lineNo = 0;
        char sep = options.Separator;
        bool handleSpeechMarks = options.HandleSpeechMarks;
        var parseHeader = options.HeaderMode;
        if (parseHeader == CSVHeaderMode.Parse)
        {
            int headerLen = ReadLine(stream, line);
            if (headerLen != 0)
            {
                fields = ReadHeader(line.AsSpan()[..headerLen]);
            }
            lineNo++;
        }
        else if (parseHeader == CSVHeaderMode.Skip)
        {
            ReadLine(stream, line);
            lineNo++;
        }

        RowWorker[]? rowWorkers = null;
        CircularBuffer<WorkItem>? workQueue = null;
        ConcurrentBag<char[]>? lineBuffers = null;
        if (options.Multithreaded)
        {
            // Create the work queue and preallocate char buffers in each of it's slots to be reused later.
            workQueue = new(128);
            /*for (int i = 0; i < workQueue.Capacity; i++)
                workQueue.Enqueue(new(new char[options.MaximumLineSize]));
            for (int i = 0; i < workQueue.Capacity; i++)
                workQueue.Dequeue();*/
            lineBuffers = new(Enumerable.Range(0, workQueue.Capacity + 8).Select(x => new char[options.MaximumLineSize]));

            rowWorkers = new RowWorker[Environment.ProcessorCount - 1];
            for (int i = 0; i < rowWorkers.Length; i++)
                rowWorkers[i] = new RowWorker(options, fields, workQueue, lineBuffers);
        }

        var headerData = new HeaderData<T>(fields, sep, handleSpeechMarks, lineNo, rowWorkers, workQueue, lineBuffers);

        return headerData;
    }

    /// <summary>
    /// Parses a CSV file into an IEnumerable of rows.
    /// </summary>
    /// <typeparam name="T">The row data structure to deserialize into.</typeparam>
    /// <param name="stream">The binary stream to read from.</param>
    /// <param name="leaveOpen">Whether the stream should be left open after parsing has finished.</param>
    /// <returns>An enumerable of rows, parsed as they are iterated through.</returns>
    public IEnumerable<T> Parse(Stream stream, bool leaveOpen = true)
    {
        try
        {
            var header = Initialise(stream);

            if (options.Multithreaded)
                return ParseBodyThreaded(stream, header);
            else
                return ParseBodySimple(stream, header.typeInfo, header.lineNo);
        }
        finally
        {
            if (!leaveOpen)
                stream.Dispose();
        }
    }

    /// <summary>
    /// Parses a CSV file into an array of items.
    /// </summary>
    /// <param name="headerData">The header data returned by the last call to the <see cref="Initialise(Stream)"/> method.</param>
    /// <param name="stream">The binary stream to read from.</param>
    /// <param name="destination">The array to parse rows into.</param>
    /// <param name="index">The starting index of the array.</param>
    /// <returns><c>true</c> if there are more rows to parse </returns>
    public bool Parse(ref HeaderData<T> headerData, Stream stream, T[] destination, int index)
    {
        if (options.Multithreaded)
            return ParseBodyThreaded(destination, index, stream, ref headerData);
        else
        {
            bool moreToRead = true;
            for (int i = 0; i < destination.Length; i++)
                moreToRead = ParseRow(ref headerData, stream, ref destination[i]);

            return moreToRead;
        }
    }

    /// <summary>
    /// Reads and parses a single row from the CSV file.
    /// </summary>
    /// <param name="headerData">The header data returned by the last call to the <see cref="Initialise(Stream)"/> method.</param>
    /// <param name="stream">A binary stream containing the CSV file to be parsed.</param>
    /// <param name="result">The object to populate with the data from the parsed CSV row.</param>
    /// <returns><c>true</c> if there are more rows to read, otherwise <c>false</c></returns>
    public bool ParseRow(ref HeaderData<T> headerData, Stream stream, ref T result)
    {
        int len;
        while ((len = ReadLine(stream, line)) != -1)
        {
            if (len == 0)
                continue;

            ReadRow(ref result, line.AsSpan()[..len], lineUnescaped, headerData.sep, headerData.handleSpeechMarks, headerData.typeInfo, headerData.lineNo);
            headerData.lineNo++;

            return true;
        }

        return false;
    }

    private IEnumerable<T> ParseBodySimple(Stream stream, ReflectionData<T>?[] fields, int lineNo)
    {
        char sep = options.Separator;
        bool handleSpeechMarks = options.HandleSpeechMarks;
        int len;
        while ((len = ReadLine(stream, line)) != -1)
        {
            if (len == 0)
                continue;
            T ret = new T();
            ReadRow(ref ret, line.AsSpan()[..len], lineUnescaped, sep, handleSpeechMarks, fields, lineNo);
            yield return ret;
            lineNo++;
        }
    }

    private IEnumerable<T> ParseBodyThreaded(Stream stream, HeaderData<T> header)
    {
        T[] retBuff = new T[64];
        //if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
        for (int i = 0; i < retBuff.Length; i++)
            retBuff[i] = new T();

        while (true)
        {
            bool remaining = ParseBodyThreaded(retBuff, 0, stream, ref header);
            for (int i = 0; i < retBuff.Length; i++)
                yield return retBuff[i];

            if (!remaining)
                break;
        }
    }

    private bool ParseBodyThreaded(T[] destination, int index, Stream stream, ref HeaderData<T> header)
    {
        var rowWorkers = header.rowWorkers!;
        var workQueue = header.workQueue!;
        int lineNo = header.lineNo;
        int lineNoStart = lineNo;
        //int linesParsed = 0;
        for (int i = 0; i < rowWorkers.Length; i++)
        {
            rowWorkers[i].destination = destination;
            rowWorkers[i].Start();
            /*rowWorkers[i].onComplete = _ =>
            {
                Interlocked.Increment(ref linesParsed);
            };*/
        }

        int len;
        do
        {
            char[]? lineBuff;
            while (!header.charBuffers!.TryTake(out lineBuff))
                Thread.Yield();
            
            len = ReadLine(stream, lineBuff);

            WorkItem work = new(lineBuff, len, lineNo, lineNo - lineNoStart + index);
            workQueue.Add(work);

            lineNo++;
            // No more space in the output buffer, wait for results and return
            int linesRead = lineNo - lineNoStart;
            if (linesRead + index >= destination.Length)
            {
                for (int i = 0; i < rowWorkers.Length; i++)
                    rowWorkers[i].Stop();

                header.lineNo = lineNo;
                header.lineNoStart = lineNo;
                return true;
            }
        } while (len != -1);

        // No more lines to parse, wait for results, and dispose of threads
        foreach (var worker in rowWorkers)
            worker.Dispose();

        header.lineNo = lineNo;
        header.lineNoStart = lineNo;
        return false;
    }

    [RequiresUnreferencedCode("Parsing CSV file using reflection.")]
    private ReflectionData<T>?[] BuildReflectionCache()
    {
        List<ReflectionData<T>?> reflection = [];
        bool needsSorting = false;

        int addedIndex = 0;
        if (options.IncludeFields)
        {
            var fields = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public | (options.IncludePrivate ? BindingFlags.NonPublic : BindingFlags.Default));
            foreach (var field in fields)
            {
                var refl = ReflectionData<T>.Create(field, addedIndex++);
                if (refl != null)
                    reflection.Add(refl);
            }
        }
        if (options.IncludeProperties)
        {
            var fields = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public | (options.IncludePrivate ? BindingFlags.NonPublic : BindingFlags.Default));
            foreach (var field in fields)
            {
                var refl = ReflectionData<T>.Create(field, addedIndex++);
                if (refl != null)
                    reflection.Add(refl);
            }
        }

        if (true || needsSorting)
        {
            var sorted = reflection.OrderBy(x => x!.Value.index);
            List<ReflectionData<T>?> expanded = [];
            int lastInd = -1;
            foreach (var field in sorted)
            {
                int ind = field!.Value.index;
                int diff = ind - lastInd - 1;
                lastInd = ind;
                for (int i = 0; i < diff; i++)
                    expanded.Add(null);
                expanded.Add(field);
            }
            reflection = expanded;
        }

        return [.. reflection];
    }

    private ReflectionData<T>?[] ReadHeader(ReadOnlySpan<char> header)
    {
        List<ReflectionData<T>?> res = [];
        int start = 0;

        // Check for a UTF BOM and skip it...
        if (header.Length > 0 && header[0] == '\uFEFF')
            start++;

        char sep = options.Separator;
        while (true)
        {
            if (start >= header.Length)
                break;

            var col = header[start..];
            int end = col.IndexOf(sep);
            //if (end == -1)
            //    break;

            if (end != -1)
                col = col[..end];
            bool found = false;
            for (int i = 0; i < typeInfo.Length; i++)
            {
                if (typeInfo[i] is ReflectionData<T> t)
                {
                    var name = t.csvName ?? t.fieldName;
                    if (col.SequenceEqual(name))
                    {
                        res.Add(t);
                        found = true;
                        break;
                    }
                }
            }
            if (!found)
                res.Add(null);

            if (end == -1)
                break;

            start += end + 1;
        }

        return [.. res];
    }

    private static void ReadRow(ref T result, ReadOnlySpan<char> line, char[] lineUnescaped, char sep, bool handleSpeechMarks, ReflectionData<T>?[] fields, int lineNo)
    {
        //T ret = new T();
        /*object retBox = ret;
        if (typeof(T).IsValueType)
            Unsafe.Unbox<T>(retBox);*/

        int start = 0;
        int ind = 0;
        while (true)
        {
            if (start >= line.Length || ind > fields.Length)
                break;

            var item = line[start..];
            int end = -1;

            if (handleSpeechMarks && item.Length > 0 && item[0] == '"')
            {
                int unescLen = 0;
                Span<char> unesc = lineUnescaped.AsSpan();
                item = item[1..];
                // Find more quotation marks
                int pos;
                while ((pos = item.IndexOf('"')) != -1)
                {
                    if (pos + 1 < item.Length)
                    {
                        char next = item[pos + 1];
                        if (next == '"')
                        {
                            // Unescape "
                            // Copy from the start of the line to the quote
                            item[..(pos + 1)].CopyTo(unesc[unescLen..]);
                            unescLen += pos + 1;
                            end += pos + 2;

                            // Trim the start of the line till after both quotes
                            item = item[(pos + 2)..];
                        }
                        else if (next == sep || next == '\r' || next == '\n') // TODO: Remove the line ending test from here once the line reader bugs have been ironed out...
                        {
                            // Reached end of field
                            //Cpy
                            item[..pos].CopyTo(unesc[unescLen..]);
                            unescLen += pos;
                            end += pos + 2;
                            break;
                        }
                        else
                            throw new CSVSerializerException($"Illegal quotation mark found in CSV file, quotation marks can only appear at the start or end of fields. " +
                                $"At line number {lineNo + 1} line: \n{line}");
                    }
                    else
                    {
                        // Reached end of line
                        //Cpy
                        item[..pos].CopyTo(unesc[unescLen..]);
                        unescLen += pos;
                        end += unescLen;
                        break;
                    }
                }

                item = unesc[..unescLen];
                end++;
            }
            else
            {
                end = item.IndexOf(sep);
                //if (end == -1)
                //    break;
                if (end != -1)
                    item = item[..end];
            }

            if (ind < fields.Length)
            {
                //throw new CSVSerializerException($"Row at line {lineNo + 1} has too many fields! Expected {fields.Length}. Line: \n{line}");

                var field = fields[ind];
                if (field is ReflectionData<T> f)
                {
                    //object? val = null;
                    if (f.customDeserializer != null)
                    {
                        object? val = f.customDeserializer.Deserialize(item, lineNo);
                        f.setValueFunc(ref result, val);
                    }
                    else
                    {
                        if (f.isNullable)
                        {
                            if (item.Length == 0)
                                f.setValueFunc(ref result, null);
                            else
                                try
                                {
                                    f.deserializeValueFunc(ref result, item);
                                    //val = DeserializeBasicItem(item, f.fieldType, lineNo, f.fieldName);
                                }
                                catch (CSVSerializerException) { }
                                catch (FormatException) { }
                                catch (OverflowException) { }
                        }
                        else
                        {
                            try
                            {
                                f.deserializeValueFunc(ref result, item);
                            }
                            catch (Exception ex) when (ex is FormatException or OverflowException)
                            {
                                throw new CSVSerializerException($"Value of '{line}' couldn't be parsed as a {f.fieldType.Name} {f.fieldName}! At line number {lineNo + 1}.", ex);
                            }
                        }
                    }

                    //f.field?.SetValue(ret, val);
                }
            }

            start += end + 1;
            ind++;
        }

        //return ret;
    }

    #region Multithreading
    internal struct WorkItem(char[]? line, int lineLen, int lineNo, int destInd)
    {
        // TODO: it might make sense if a single work item contained multiple lines?
        public char[]? line = line;
        public int lineLen = lineLen;
        public int lineNo = lineNo;
        public int destInd = destInd;

        public WorkItem(char[]? line) : this(line, 0, 0, 0)
        {
            
        }
    }

    internal class RowWorker : IDisposable
    {
        public T[] destination;
        public Action<int>? onComplete;

        private volatile bool terminate;

        private readonly Thread workThread;
        private readonly char[] lineUnescaped;
        private readonly char sep;
        private readonly bool handleSpeechMarks;
        private readonly ReflectionData<T>?[] fields;
        private readonly CircularBuffer<WorkItem> workQueue;
        private readonly ConcurrentBag<char[]> lineBuffers;

        public RowWorker(CSVSerializerOptions options, ReflectionData<T>?[] fields, 
            CircularBuffer<WorkItem> workQueue, ConcurrentBag<char[]> lineBuffers, 
            Action<int>? onComplete = null)
        {
            lineUnescaped = new char[options.MaximumLineSize];
            sep = options.Separator;
            handleSpeechMarks = options.HandleSpeechMarks;
            this.fields = fields;
            this.destination = [];
            this.workQueue = workQueue;
            this.lineBuffers = lineBuffers;
            this.onComplete = onComplete;

            workThread = new(Work);
            //workThread.Start();
        }

        public void Start()
        {
            terminate = false;
            if (workThread.ThreadState != ThreadState.Running)
                workThread.Start();
        }

        public void Stop()
        {
            terminate = true;
            workThread.Join();
        }

        private void Work()
        {
            SpinWait spinner = default;
            while (!terminate)
            {
                WorkItem work;
                while (!workQueue.TryDequeue(out work) && !terminate)
                    //Thread.SpinWait(1);
                    spinner.SpinOnce(-1);
                spinner.Reset();

                if (work.lineLen == 0)
                    continue;

                //T ret = new T();
                ref var ret = ref destination[work.destInd];
                ReadRow(ref ret, work.line.AsSpan()[..work.lineLen], lineUnescaped, sep, handleSpeechMarks, fields, work.lineNo);

                //Volatile.Write(ref Unsafe.As<char, ushort>(ref work.line![0]), 0);
                // Return the char buffer to the pool of free buffers
                lineBuffers.Add(work.line!);

                onComplete?.Invoke(work.lineNo);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
    #endregion

    #region Line Reader
    private enum LineEnding
    {
        None,
        UnixCR,
        MacLF,
        WindowsCRLF
    }

    /// <summary>
    /// Gets the position in bytes in the given of this CSV Parser, taking into account the internal state of the parser.
    /// </summary>
    /// <param name="stream">The stream that was last read from.</param>
    /// <returns>The position in bytes in the stream after the last row was read.</returns>
    public long GetPosition(Stream stream)
    {
        var buffSpan = charBuffer.AsSpan()[position..charBuffLength];
        int charBuffBytesLeft = encoding.GetByteCount(buffSpan);
        return stream.Position - charBuffBytesLeft;
    }

    /// <summary>
    /// Reads a line of characters into the given span (not including the line break).
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <param name="dst">The span to read the line into.</param>
    /// <returns>The number of characters read; if this is greater than the length of the 
    /// input span, then the full line couldn't be fit into the span. Returns -1 if the end of the stream was reached.</returns>
    private int ReadLine(Stream stream, Span<char> dst)
    {
#if DEBUG
        if (dst.Length == 0)
            throw new ArgumentException("Attempted to read line into a 0-length span!");
#endif
        // TODO: since dst is always an array, we could support arbitrary line lengths by growing our line buffer as needed.

        //var buff = charBuffer.AsMemory()[position..];
        var buffSpan = charBuffer.AsSpan()[position..charBuffLength];

        FindLineBreak(buffSpan, out int len, out LineEnding lineEnding);

        // Fast path
        int read = 0;
        int written = 0;
        if (lineEnding != LineEnding.None)
        {
            int crLen = (lineEnding == LineEnding.WindowsCRLF ? 2 : 1);
            read = len - crLen;
            if (read <= dst.Length)
            {
                buffSpan[..read].CopyTo(dst);
                position += len;
            }
            else
            {
                buffSpan[..dst.Length].CopyTo(dst);
                position += dst.Length;
            }
            return read;
        }

        // Copy everything to the dst span
        read = buffSpan.Length;
        if (read > 0)
        {
            written = Math.Min(read, dst.Length);
            buffSpan[..written].CopyTo(dst);
            position += written;
        }

        // Keep reading more data into dst until we find a line break or dst is full
        int dstPos = written;
        while (written < dst.Length)
        {
            if (!FillCharBuff(stream))
                return read == 0 ? -1 : read;

            buffSpan = charBuffer.AsSpan()[..charBuffLength];

            FindLineBreak(buffSpan, out len, out lineEnding);

            int dstSpace = dst.Length - dstPos;
            if (lineEnding != LineEnding.None)
            {
                int crLen = (lineEnding == LineEnding.WindowsCRLF ? 2 : 1);
                int justRead = len - crLen;
                read += justRead;
                if (justRead <= dstSpace)
                {
                    buffSpan[..justRead].CopyTo(dst[dstPos..]);
                    position += len;
                }
                else
                {
                    buffSpan[..dstSpace].CopyTo(dst[dstPos..]);
                    position += dstSpace;
                }
                return read;
            }
            else
            {
                read += buffSpan.Length;
                if (buffSpan.Length <= dstSpace)
                {
                    buffSpan.CopyTo(dst[dstPos..]);
                    dstPos += buffSpan.Length;
                }
                else
                {
                    // No more space...
                    buffSpan[..dstSpace].CopyTo(dst[dstPos..]);
                    return read;
                }
            }
        }
        return read;
    }

    private static void FindLineBreak(Span<char> buff, out int len, out LineEnding lineEnding)
    {
        // \n - UNIX  \r\n - Windows  \r - Mac
        lineEnding = LineEnding.None;
        len = buff.IndexOfAny('\n', '\r');
        //len = buff.IndexOf('\n');

        if (len == -1)
            return;

        char c = buff[len];
        len++;
        if (c == '\n')
        {
            lineEnding = LineEnding.UnixCR;
        }
        else // c == '\r'
        {
            if (len < buff.Length)
            {
                char c1 = buff[len];
                if (c1 == '\n')
                {
                    lineEnding = LineEnding.WindowsCRLF;
                    len++;
                }
                else
                {
                    lineEnding = LineEnding.MacLF;
                }
            }
        }
    }

    /// <summary>
    /// Fills the char buffer with new characters resetting the charBuffLength and position variables.
    /// </summary>
    /// <returns>false if no bytes could be read from the stream.</returns>
    private bool FillCharBuff(Stream stream)
    {
        charBuffLength = 0;
        position = 0;
        int bytesRead = stream.Read(byteBuffer);
        if (bytesRead == 0)
            return false; // No more bytes to read

        var bytesSpan = byteBuffer.AsSpan()[..bytesRead];
        //int buffLen = bytesRead;
        //int buffOffset = 0;
        if (!detectedEncoding)
        {
            int preamble = DetectEncoding(bytesRead);
            bytesSpan = bytesSpan[preamble..];
            //buffLen -= preamble;
            //buffOffset = preamble;
        }

        charBuffLength = decoder.GetChars(bytesSpan, charBuffer, false);
        // The version of this method that takes a span copies into a new temp array
        //charBuffLength = decoder.GetChars(byteBuffer, buffOffset, buffLen, charBuffer, 0, false);

        return true;
    }

    // Derived from C# reference source StreamReader.cs, Copyright Microsoft
    // Returns the number of preamble bytes to trim
    private int DetectEncoding(int bytesRead)
    {
        if (bytesRead < 2)
            return 0;

        int preamble = 0;
        bool changedEncoding = false;
        ushort firstTwoBytes = BinaryPrimitives.ReadUInt16LittleEndian(byteBuffer);
        if (firstTwoBytes == 0xFFFE)
        {
            // Big Endian Unicode
            encoding = Encoding.BigEndianUnicode;
            changedEncoding = true;
            preamble = 2;
        }
        else if (firstTwoBytes == 0xFEFF)
        {
            // Little Endian Unicode, or possibly little endian UTF32
            if (bytesRead < 4 || byteBuffer[2] != 0 || byteBuffer[3] != 0)
            {
                encoding = Encoding.Unicode;
                changedEncoding = true;
                preamble = 2;
            }
            else
            {
                encoding = Encoding.UTF32;
                changedEncoding = true;
                preamble = 4;
            }
        }
        else if (bytesRead >= 4 && firstTwoBytes == 0 && byteBuffer[2] == 0xFE && byteBuffer[3] == 0xFF)
        {
            // Big Endian UTF32
            encoding = new UTF32Encoding(bigEndian: true, byteOrderMark: true);
            changedEncoding = true;
            preamble = 4;
        }
        else if (bytesRead == 2)
        {
            detectedEncoding = false;
        }

        if (changedEncoding)
        {
            decoder = encoding.GetDecoder();
            int newMaxCharsPerBuffer = encoding.GetMaxCharCount(byteBuffer.Length);
            if (newMaxCharsPerBuffer > charBuffer.Length)
                charBuffer = new char[newMaxCharsPerBuffer];
        }

        detectedEncoding = true;
        return preamble;
    }
    #endregion
}

/// <summary>
/// Implements the methods needed to serialize/deserialize a given type as a CSV field.
/// </summary>
public interface ICustomCSVSerializer
{
    public object? Deserialize(ReadOnlySpan<char> data, int lineNumber);
    public ReadOnlySpan<char> Serialize(object? data, int lineNumber) => data?.ToString();
}

/// <summary>
/// Supports serialization and deserialization by the CSV parser. Note that implementors must also provide a constructor 
/// which takes a single <see cref="ReadOnlySpan{char}"/> of <see cref="char"/> as a parameter.
/// </summary>
public interface ICSVSerializable
{
    //public abstract ICSVSerializable(ReadOnlySpan<char> data);

    public int Serialize(Span<char> dst);
}

/// <summary>
/// The CSV parser's behaviour when reading a CSV header row.
/// </summary>
public enum CSVHeaderMode
{
    /// <summary>
    /// The file does not contain a header row.
    /// </summary>
    None,
    /// <summary>
    /// The file contains a header row which should be parsed to associate the columns with the fields/properties of the target data structure.
    /// </summary>
    Parse,
    /// <summary>
    /// The file contains a header row which should be ignored.
    /// </summary>
    Skip
}

/// <summary>
/// Represents an exception that occurred while parsing a CSV file.
/// </summary>
public class CSVSerializerException : Exception
{
    public CSVSerializerException() : base() { }
    public CSVSerializerException(string message) : base(message) { }
    public CSVSerializerException(string message, Exception? innerException) : base(message, innerException) { }
}

/// <summary>
/// Specifies the column name as defined in the CSV header to match this field to.
/// </summary>
/// <param name="name">The column name to match.</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CSVNameAttribute(string name) : Attribute
{
    readonly string name = name;

    public string Name => name;
}

/// <summary>
/// Specifies the column index as defined in the CSV header to match this field to.
/// </summary>
/// <param name="name">The column name to match.</param>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CSVIndexAttribute(int index) : Attribute
{
    readonly int index = index;

    public int Index => index;
}

/// <summary>
/// Serializes/deserializes the field using a custom serializer.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CSVCustomSerializerAttribute<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>() : Attribute where T : ICustomCSVSerializer { }

/// <summary>
/// Marks a field as skipped while parsing the CSV file.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class CSVSkipAttribute() : Attribute { }
