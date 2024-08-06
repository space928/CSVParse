using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CSVParse;

public static class CSVParser
{
    public static IEnumerable<T> Parse<T>(Stream stream, CSVHeaderMode parseHeader = CSVHeaderMode.Parse, bool leaveOpen = true, Encoding? encoding = null) where T : new()
    {
        return new CSVParser<T>().Parse(stream, parseHeader, leaveOpen, encoding);
    }
}

public class CSVSerializerOptions
{
    public bool IncludeFields { get; init; } = false;
    public bool IncludeProperties { get; init; } = true;
    public bool IncludePrivate { get; init; } = false;
    public bool HandleSpeechMarks { get; init; } = false;
    public char Separator { get; init; } = ',';

    public static readonly CSVSerializerOptions Default = new();
}

[RequiresUnreferencedCode("")]
public partial class CSVParser<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields
        | DynamicallyAccessedMemberTypes.NonPublicFields
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.NonPublicProperties)] T> where T : new()
{
    private readonly byte[] byteBuffer;
    private readonly char[] line;
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

    public CSVParser(CSVSerializerOptions? options = null)
    {
        byteBuffer = new byte[defaultReadSize];
        line = new char[defaultReadSize / 2];
        this.options = options ?? CSVSerializerOptions.Default;
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

    public IEnumerable<T> Parse(Stream stream, CSVHeaderMode parseHeader = CSVHeaderMode.Parse, bool leaveOpen = true, Encoding? encoding = null)
    {
        try
        {
            Init(encoding);

            //Span<char> line = stackalloc char[2048];
            var fields = typeInfo;
            int lineNo = 0;
            char sep = options.Separator;
            bool handleSpeechMarks = options.HandleSpeechMarks;
            if (parseHeader == CSVHeaderMode.Parse)
            {
                int headerLen = ReadLine(stream, line);
                if (headerLen == 0)
                    yield break;
                fields = ReadHeader(line.AsSpan()[..headerLen]);
                lineNo++;
            }
            else if (parseHeader == CSVHeaderMode.Skip)
            {
                ReadLine(stream, line);
                lineNo++;
            }

            int len = 0;
            while ((len = ReadLine(stream, line)) != -1)
            {
                if (len == 0)
                    continue;
                yield return ReadRow(line.AsSpan()[..len], sep, handleSpeechMarks, fields, lineNo);
                lineNo++;
            }
        }
        finally
        {
            if (!leaveOpen)
                stream.Dispose();
        }
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
        char sep = options.Separator;
        while (true)
        {
            if (start >= header.Length)
                break;

            var col = header[start..];
            int end = col.IndexOf(sep);
            if (end == -1)
                break;

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

            start += end + 1;
        }

        return [.. res];
    }

    private static T ReadRow(ReadOnlySpan<char> line, char sep, bool handleSpeechMarks, ReflectionData<T>?[] fields, int lineNo)
    {
        T ret = new T();
        /*object retBox = ret;
        if (typeof(T).IsValueType)
            Unsafe.Unbox<T>(retBox);*/

        if (handleSpeechMarks)
        {

        }
        else
        {
            int start = 0;
            int ind = 0;
            while (true)
            {
                if (start >= line.Length || ind > fields.Length)
                    break;

                var item = line[start..];
                int end = item.IndexOf(sep);
                if (end == -1)
                    break;

                item = item[..end];
                var field = fields[ind];
                if (field is ReflectionData<T> f)
                {
                    //object? val = null;
                    if (f.customDeserializer != null)
                    {
                        object? val = f.customDeserializer.Deserialize(item, lineNo);
                        f.setValueFunc(ref ret, val);
                    }
                    else
                    {
                        if (f.isNullable)
                        {
                            if (item.Length == 0)
                                f.setValueFunc(ref ret, null);
                            else
                                try
                                {
                                    f.deserializeValueFunc(ref ret, item);
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
                                f.deserializeValueFunc(ref ret, item);
                            }
                            catch (Exception ex) when (ex is FormatException or OverflowException)
                            {
                                throw new CSVSerializerException($"Value of '{line}' couldn't be parsed as a {f.fieldType.Name} {f.fieldName}! At line number {lineNo + 1}.", ex);
                            }
                        }
                    }
                    
                    //f.field?.SetValue(ret, val);
                }

                start += end + 1;
                ind++;
            }
        }

        return ret;
    }

    private enum LineEnding
    {
        None,
        UnixCR,
        MacLF,
        WindowsCRLF
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
            if (len + 1 < buff.Length)
            {
                char c1 = buff[len + 1];
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
}

public interface ICustomCSVSerializer
{
    public object? Deserialize(ReadOnlySpan<char> data, int lineNumber);
    public ReadOnlySpan<char> Serialize(object? data, int lineNumber) => data?.ToString();
}

public enum CSVHeaderMode
{
    None,
    Parse,
    Skip
}

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
