using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using O3DParse;
using System.Runtime.CompilerServices;
using System.IO.MemoryMappedFiles;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Numerics;

namespace CSVParse;

//[SimpleJob(RuntimeMoniker.Net60, baseline: true)]
//[SimpleJob(RuntimeMoniker.NetCoreApp30, launchCount: -1, warmupCount: 4, iterationCount: 10)]
//[SimpleJob(RuntimeMoniker.NativeAot80, launchCount: -1, warmupCount: 4, iterationCount: 10)]
[SimpleJob(RuntimeMoniker.Net80, launchCount:-1, warmupCount:4, iterationCount:10)]
[MemoryDiagnoser]
//[DryJob(RuntimeMoniker.Net80)]
//[DisassemblyDiagnoser(printSource:true, exportHtml:true)]
//[RPlotExporter]
public class LineReaderBenchmarks
{
    public LineReaderBenchmarks()
    {
        string basePath = @"D:\Thoma\Documents\OneDrive\Computer Sync\visual studio 2015\Projects\CSVParse\CSVParse\bin\Release\net8.0\";
        string[] filenames = [
            "1shortLinesWinShort.txt",
            "2shortLinesUnixShort.txt",
            "3shortLinesWinLong.txt",
            "4shortLinesUnixLong.txt",
            "5longLinesWinShort.txt",
            "6longLinesUnixShort.txt",
            "7longLinesWinLong.txt",
            "8longLinesUnixLong.txt",
            "9shortLinesUnixVeryLong.txt",
        ];

        // Specify the files to use in the test
        FileNames = [
            //"1shortLinesWinShort.txt",
            //"2shortLinesUnixShort.txt",
            //"3shortLinesWinLong.txt",
            "4shortLinesUnixLong.txt",
            //"5longLinesWinShort.txt",
            //"6longLinesUnixShort.txt",
            //"7longLinesWinLong.txt",
            "8longLinesUnixLong.txt",
            //"9shortLinesUnixVeryLong.txt",
        ];

        for (int i = 0; i < FileNames.Length; i++)
        {
            FileNames[i] = Path.Combine(basePath, FileNames[i]);
        }

        var allExist = filenames.Select(x => File.Exists(Path.Combine(basePath, x))).All(x=>x);

        if (!allExist)
        {
            // Create a few files to read
            Console.WriteLine("Generating test data..."); ;
            var shortLines = GenerateRandomLines(100_0000, 0, 8);
            var longLines = GenerateRandomLines(100_0000, 0, 1024);
            var manyShortLines = GenerateRandomLines(20_000_0000, 0, 16);
            /*string shortLinesWinShort = string.Join("\r\n", shortLines.Take(64).Append("DONE____DONE"));
            string shortLinesUnixShort = string.Join('\n', shortLines.Take(64).Append("DONE____DONE"));
            string shortLinesWinLong = string.Join("\r\n", shortLines.Append("DONE____DONE"));
            string shortLinesUnixLong = string.Join('\n', shortLines.Append("DONE____DONE"));
            string shortLinesUnixVeryLong = string.Join('\n', manyShortLines.Append("DONE____DONE"));
            string longLinesWinShort = string.Join("\r\n", longLines.Take(64).Append("DONE____DONE"));
            string longLinesUnixShort = string.Join('\n', longLines.Take(64).Append("DONE____DONE"));
            string longLinesWinLong = string.Join("\r\n", longLines.Append("DONE____DONE"));
            string longLinesUnixLong = string.Join('\n', longLines.Append("DONE____DONE"));*/

            Console.WriteLine("Writing to disk...");

            Task.WhenAll(
                WriteLinesIfNew(basePath, filenames[0], shortLines.Take(64).Append("DONE____DONE"), "\r\n"),
                WriteLinesIfNew(basePath, filenames[1], shortLines.Take(64).Append("DONE____DONE"), "\n"),
                WriteLinesIfNew(basePath, filenames[2], shortLines.Append("DONE____DONE"), "\r\n"),
                WriteLinesIfNew(basePath, filenames[3], shortLines.Append("DONE____DONE"), "\n"),
                WriteLinesIfNew(basePath, filenames[4], longLines.Take(64).Append("DONE____DONE"), "\r\n"),
                WriteLinesIfNew(basePath, filenames[5], longLines.Take(64).Append("DONE____DONE"), "\n"),
                WriteLinesIfNew(basePath, filenames[6], longLines.Append("DONE____DONE"), "\r\n"),
                WriteLinesIfNew(basePath, filenames[7], longLines.Append("DONE____DONE"), "\n"),
                WriteLinesIfNew(basePath, filenames[8], manyShortLines.Append("DONE____DONE"), "\n")
            ).Wait();

            Console.WriteLine("GC...");
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        } 
        else
        {
            Console.WriteLine("Test data already exists...");
        }

        Console.WriteLine("DONE! Start test...");
    }

    private async Task WriteLinesIfNew(string basePath, string filename, IEnumerable<string> text, string sep)
    {
        string path = Path.Combine(basePath, filename);

        if (File.Exists(path))
            return;

        using var fs = new StreamWriter(path);
        foreach(var line in text)
        {
            await fs.WriteAsync(line + sep);
            //await fs.WriteAsync(sep);
        }

        //await File.WriteAllTextAsync(path, text);
    }

    [GlobalSetup]
    public void Setup()
    {
        
    }

    private const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWZYXabcdefghijklmnopqrstuvwxyz    12345678901234567890-=+}{#';/.,!£%^$&*)()";
    private static IEnumerable<string> GenerateRandomLines(int n, int minLength, int maxLength)
    {
        //List<string> lines = [];
        StringBuilder sb = new();
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < Random.Shared.Next(minLength, maxLength); j++)
            {
                sb.Append(alphabet[Random.Shared.Next(alphabet.Length)]);
            }
            yield return sb.ToString();
            //lines.Add(sb.ToString());
            sb.Clear();
        }
        //return [.. lines];
    }

    [ParamsSource(nameof(FileNames))]
    public string? FileName { get; set; }

    public string[] FileNames { get; set; }

    [Benchmark(Baseline = true)]
    public string TestReadLines()
    {
        var lines = File.ReadLines(FileName!);

        StringBuilder sb = new();
        foreach (var item in lines)
            sb.Append(item);
        return sb.ToString();
    }

    //[Benchmark]
    public string TestReadAllLines()
    {
        var lines = File.ReadAllLines(FileName!);

        StringBuilder sb = new();
        foreach (var item in lines)
            sb.Append(item);
        return sb.ToString();
    }

    //[Benchmark]
    public string TestStreamReader()
    {
        using var reader = new StreamReader(FileName!);

        StringBuilder sb = new();
        while (reader.ReadLine() is string line)
            sb.Append(line);
        return sb.ToString();
    }

    //[Benchmark]
    public string TestBufferedStreamReader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        using var buffer = new BufferedStream(fs);
        using var reader = new StreamReader(buffer);

        StringBuilder sb = new();
        while (reader.ReadLine() is string line)
            sb.Append(line);
        return sb.ToString();
    }

    [Benchmark]
    public string TestOldIniFileReader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        using var ini = new IniFileReaderOld(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = ini.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    [Benchmark]
    public string TestIniFileReader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        using var ini = new IniFileReader(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = ini.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    [Benchmark]
    public string TestFastReader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        var reader = new FastReader(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = reader.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    [Benchmark]
    public string TestFastUTF8Reader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        var reader = new FastUTF8Reader(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = reader.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    [Benchmark]
    public string TestFastUTF8Reader1()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        var reader = new FastUTF8Reader1(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = reader.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    [Benchmark]
    public string TestFastVectorReader()
    {
        using var fs = new FileStream(FileName!, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
        var reader = new FastVectorReader(fs);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = reader.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    //[Benchmark]
    public string TestFastMemoryMappedReader()
    {
        using var reader = new FastMemoryMappedReader(FileName!);
        Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        int read = 0;
        while ((read = reader.ReadLine(line)) != -1)
            sb.Append(line[..read]);
        return sb.ToString();
    }

    //[Benchmark]
    public string TestFastPipelinedReader()
    {
        using var reader = new FastPipelinedReader(FileName!);
        //Span<char> line = stackalloc char[2048];

        StringBuilder sb = new();
        reader.ProcessLinesAsync((line) =>
        {
            if (line.IsSingleSegment)
                foreach (var b in line.FirstSpan)
                    sb.Append((char)b);
            else
            {
                foreach (var part in line)
                    foreach (var b in part.Span)
                        sb.Append((char)b);
            }
        }).Wait();

        return sb.ToString();
    }
}

public class FastReader
{
    private int buffPos;
    private int buffLen;
    private byte[] buffer;
    private Stream stream;

    public FastReader(Stream stream)
    {
        buffer = new byte[4096];
        buffPos = 0;
        buffLen = 0;
        this.stream = stream;
    }

    public int ReadLine(Span<char> dst)
    {
        if (buffPos == buffLen)
        {
            if (ReadBuff() == 0)
                return -1;
        }

        int linePos = 0;
        do
        {
            var buff = buffer.AsSpan()[buffPos..buffLen];
            int endPos;
            if ((endPos = buff.IndexOf((byte)'\n')) != -1)
            {
                for (int i = 0; i < endPos; i++)
                    dst[linePos++] = (char)buff[i];
                buffPos += endPos+1;
                return linePos;
            }

            for (int i = 0; i < buff.Length; i++)
                dst[linePos++] = (char)buff[i];
            buffPos = buff.Length;
        } while (ReadBuff() != 0);

        return linePos;
    }

    private int ReadBuff()
    {
        int read = stream.Read(buffer, 0, buffer.Length);
        buffLen = read;
        buffPos = 0;
        return read;
    }
}

public class FastUTF8Reader
{
    private int buffPos;
    private int buffLen;
    private readonly byte[] buffer;
    private readonly Stream stream;
    private readonly Encoding encoding;
    private readonly Decoder decoder;

    public FastUTF8Reader(Stream stream)
    {
        buffer = new byte[4096];
        buffPos = 0;
        buffLen = 0;
        this.stream = stream;
        encoding = Encoding.UTF8;
        //detectedEncoding = encoding != null;
        decoder = encoding.GetDecoder();
    }

    public int ReadLine(Span<char> dst)
    {
        if (buffPos == buffLen)
        {
            if (ReadBuff() == 0)
                return -1;
        }

        int linePos = 0;
        do
        {
            var buff = buffer.AsSpan()[buffPos..buffLen];

            // Find a carriage return byte, because of how UT8 is encoded, we know that this byte is not part of a
            // multi-byte char (multi-byte chars must have the MSB set in each byte).
            int newLinePos = buff.IndexOf((byte)'\n');

            var convertBuff = newLinePos == -1 ? buff : buff[..newLinePos];
            //var convertLen = newLinePos == -1 ? (buffLen - buffPos) : newLinePos;
            int bytesUsed = convertBuff.Length;

            int charsUsed = decoder.GetChars(convertBuff, dst[linePos..], false);
            //decoder.Convert(buffer, buffPos, convertLen, charBuff, linePos, charBuff.Length - linePos, false, out int bytesUsed, out int charsUsed, out bool _);
            //decoder.Convert(convertBuff, dst[linePos..], false, out int bytesUsed, out int charsUsed, out bool _);
            linePos += charsUsed;

            buffPos += bytesUsed;

            if (newLinePos != -1)
            {
                buffPos++;
                //new ReadOnlySpan<char>(charBuff, 0, linePos).CopyTo(dst);
                return linePos;
            }
        } while (ReadBuff() != 0);

        //new ReadOnlySpan<char>(charBuff, 0, linePos).CopyTo(dst);
        return linePos;
    }

    private int ReadBuff()
    {
        int read = stream.Read(buffer, 0, buffer.Length);
        buffLen = read;
        buffPos = 0;
        return read;
    }
}

public class FastUTF8Reader1
{
    private int buffPos;
    private int buffLen;
    private readonly byte[] buffer;
    private readonly Stream stream;
    private readonly Encoding encoding;
    private readonly Decoder decoder;

    public FastUTF8Reader1(Stream stream)
    {
        buffer = new byte[4096];
        buffPos = 0;
        buffLen = 0;
        this.stream = stream;
        encoding = Encoding.UTF8;
        //detectedEncoding = encoding != null;
        decoder = encoding.GetDecoder();
    }

    public int ReadLine(Span<char> dst)
    {
        if (buffPos == buffLen)
        {
            if (ReadBuff() == 0)
                return -1;
        }

        int linePos = 0;
        do
        {
            var buff = buffer.AsSpan()[buffPos..buffLen];

            // Find a carriage return byte, because of how UT8 is encoded, we know that this byte is not part of a
            // multi-byte char (multi-byte chars must have the MSB set in each byte).
            int newLinePos = buff.IndexOf((byte)'\n');

            var convertBuff = newLinePos == -1 ? buff : buff[..newLinePos];
            //var convertLen = newLinePos == -1 ? (buffLen - buffPos) : newLinePos;
            int bytesUsed = convertBuff.Length;

            // Better for short lines
            // Use the fast reader conversion for ascii chars
            for (int i = 0; i < convertBuff.Length; i++)
            {
                byte b = convertBuff[i];
                if (b > 0x7f)
                {
                    convertBuff = convertBuff[i..];
                    linePos += decoder.GetChars(convertBuff, dst[linePos..], false);
                    break;
                }
                dst[linePos++] = (char)b;
            }

            /*int charsUsed = decoder.GetChars(convertBuff, dst[linePos..], false);
            //decoder.Convert(buffer, buffPos, convertLen, charBuff, linePos, charBuff.Length - linePos, false, out int bytesUsed, out int charsUsed, out bool _);
            //decoder.Convert(convertBuff, dst[linePos..], false, out int bytesUsed, out int charsUsed, out bool _);
            linePos += charsUsed;*/

            buffPos += bytesUsed;

            if (newLinePos != -1)
            {
                buffPos++;
                //new ReadOnlySpan<char>(charBuff, 0, linePos).CopyTo(dst);
                return linePos;
            }
        } while (ReadBuff() != 0);

        //new ReadOnlySpan<char>(charBuff, 0, linePos).CopyTo(dst);
        return linePos;
    }

    private int ReadBuff()
    {
        int read = stream.Read(buffer, 0, buffer.Length);
        buffLen = read;
        buffPos = 0;
        return read;
    }
}

public class FastVectorReader
{
    private int buffPos;
    private int buffLen;
    private readonly byte[] buffer;
    private readonly Stream stream;
    //private readonly Vector64<byte> newLine64;
    private readonly Vector128<byte> newLine128;
    //private readonly Vector256<byte> newLine256;

    public FastVectorReader(Stream stream)
    {
        buffer = new byte[4096];
        buffPos = 0;
        buffLen = 0;
        this.stream = stream;
        //newLine64 = Vector64.Create((byte)'\n');
        newLine128 = Vector128.Create((byte)'\n');
        //newLine256 = Vector256.Create((byte)'\n');
    }

    public int ReadLine(Span<char> dst)
    {
        if (buffPos == buffLen)
        {
            if (ReadBuff() == 0)
                return -1;
        }

        int linePos = 0;
        var newLine128_ = newLine128;
        var buffer_ = buffer;
        //var bufPos_ = buffPos;
        //var bufLen_ = buffLen;
        do
        {
            while (buffPos < buffLen)
            {
                int buffSize = buffLen - buffPos;
                /*if (buffSize >= 32)
                {
                    // Req: AVX512
                    var span = new ReadOnlySpan<byte>(buffer, buffPos, 32);
                    var vec = Vector256.Create(span);
                    var eq = Vector256.Equals(vec, newLine256).ExtractMostSignificantBits();
                    int copied = 32;
                    if (eq != 0)
                    {
                        // Find the new line position
                        copied = BitOperations.TrailingZeroCount(eq);
                    }
                    //Avx512BW.ConvertToVector512Int16()

                    var shorts = Avx512BW.ConvertToVector512Int16(vec);
                    Unsafe.WriteUnaligned(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(dst[linePos..])), shorts);
                    //shorts.CopyTo(dst[linePos..]);
                    linePos += copied;
                    buffPos += copied;
                    if (eq != 0)
                    {
                        buffPos++;
                        return linePos;
                    }
                }
                else*/
                if (buffSize >= 16)
                {
                    // Req: AVX2
                    var span = new ReadOnlySpan<byte>(buffer_, buffPos, 16);
                    var vec = Vector128.Create(span);
                    var eq = Vector128.Equals(vec, newLine128_).ExtractMostSignificantBits();
                    int copied = 16;
                    if (eq != 0)
                    {
                        // Find the new line position
                        copied = BitOperations.TrailingZeroCount(eq);
                    }

                    var shorts = Avx2.ConvertToVector256Int16(vec);
                    Unsafe.WriteUnaligned(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(dst[linePos..])), shorts);
                    //shorts.CopyTo(dst[linePos..]);
                    linePos += copied;
                    buffPos += copied;
                    if (eq != 0)
                    {
                        buffPos++;
                        return linePos;
                    }
                }
                else if (buffSize >= 8)
                {
                    // Req: SSE4.1
                    var span = new ReadOnlySpan<byte>(buffer_, buffPos, 8);
                    var vec = Vector64.Create(span).ToVector128Unsafe();
                    var eq = Vector128.Equals(vec, newLine128_).ExtractMostSignificantBits();
                    int copied = 8;
                    if (eq != 0)
                    {
                        // Find the new line position
                        copied = BitOperations.TrailingZeroCount(eq);
                    }

                    var shorts = Sse41.ConvertToVector128Int16(vec);
                    Unsafe.WriteUnaligned(ref Unsafe.As<char, byte>(ref MemoryMarshal.GetReference(dst[linePos..])), shorts);
                    //shorts.CopyTo(dst[linePos..]);
                    linePos += copied;
                    buffPos += copied;
                    if (eq != 0)
                    {
                        buffPos++;
                        return linePos;
                    }
                }
                else 
                {
                    byte b = buffer_[buffPos++];
                    if (b == '\n')
                        return linePos;
                    dst[linePos++] = (char)b;
                }
            }
        } while (ReadBuff() != 0);
        // No noticable performance benefit
        /*unsafe
        {
            fixed (byte* buff = buffer)
            {
                do
                {
                    while (buffPos < buffLen)
                    {
                        byte b = *(buff + buffPos++);
                        if (b == '\n')
                            return linePos;
                        dst[linePos++] = (char)b;
                    }
                } while (ReadBuff() != 0);
            }
        }*/

        return linePos;
    }

    private int ReadBuff()
    {
        int read = stream.Read(buffer, 0, buffer.Length);
        buffLen = read;
        buffPos = 0;
        return read;
    }
}

public class FastMemoryMappedReader : IDisposable
{
    private long buffPos;
    private readonly long buffLen;
    private readonly MemoryMappedFile file;
    private readonly MemoryMappedViewAccessor view;

    public FastMemoryMappedReader(string path)
    {
        //buffer = new byte[4096];
        buffLen = new FileInfo(path).Length+1;
        file = MemoryMappedFile.CreateFromFile(path, FileMode.Open);
        view = file.CreateViewAccessor(0, buffLen-1);
        buffPos = 0;
    }

    public void Dispose()
    {
        view?.Dispose();
        file?.Dispose();
    }

    public int ReadLine(Span<char> dst)
    {
        if (buffPos == buffLen)
            return -1;

        int linePos = 0;
        unsafe
        {
            byte* buff = null;
            view.SafeMemoryMappedViewHandle.AcquirePointer(ref buff);

            byte b = *(buff + buffPos++);
            while (b != (byte)'\n' && buffPos < buffLen)
            {
                dst[linePos++] = (char)b;

                b = *(buff + buffPos++);
            }
            view.SafeMemoryMappedViewHandle.ReleasePointer();
        }

        return linePos;
    }
}

public class FastPipelinedReader : IDisposable
{
    private readonly Pipe pipe;
    private readonly PipeReader reader;
    private readonly PipeWriter writer;
    private readonly FileStream file;

    public FastPipelinedReader(string path)
    {
        pipe = new();
        reader = pipe.Reader;
        writer = pipe.Writer;
        file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.SequentialScan);
    }

    public void Dispose()
    {
        file.Dispose();
    }

    public async Task ProcessLinesAsync(Action<ReadOnlySequence<byte>> action)
    {
        //pipe.Reset();
        Task writing = FillPipeAsync();
        Task reading = ReadPipeAsync(action);

        await Task.WhenAll(reading, writing);
    }

    private async Task FillPipeAsync()
    {
        int read;
        do
        {
            var dst = writer.GetMemory();
            read = await file.ReadAsync(dst);
            writer.Advance(read);
        } while (read != 0);

        await writer.CompleteAsync();
    }

    private async Task ReadPipeAsync(Action<ReadOnlySequence<byte>> action)
    {
        while (true)
        {
            ReadResult result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;

            while (TryReadLine(ref buffer, out ReadOnlySequence<byte> line))
                action(line);

            // Tell the PipeReader how much of the buffer has been consumed.
            reader.AdvanceTo(buffer.Start, buffer.End);

            // Stop reading if there's no more data coming.
            if (result.IsCompleted)
            {
                action(buffer);
                break;
            }
        }

        // Mark the PipeReader as complete.
        await reader.CompleteAsync();
    }

    /*private void ProcessLine(Action<Memory<char>> action, ReadOnlySequence<byte> line)
    {
        Span<char> dst = stackalloc char[(int)line.Length];
        Span<byte> buff = stackalloc byte[(int)line.Length];
        line.CopyTo(buff);
        for (int i = 0; i < dst.Length; i++)
            dst[i] = (char)buff[i];

        action(Unsafe.As<Span<char>, Memory<char>>(ref dst));
    }*/

    private bool TryReadLine(ref ReadOnlySequence<byte> buff, out ReadOnlySequence<byte> line)
    {
        SequencePosition? position = buff.PositionOf((byte)'\n');

        if (position == null)
        {
            line = default;
            return false;
        }

        line = buff.Slice(0, position.Value);
        buff = buff.Slice(buff.GetPosition(1, position.Value));
        return true;
    }
}
