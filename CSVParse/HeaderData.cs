using System.Collections.Concurrent;

namespace CSVParse;

/// <summary>
/// Stores metadata parsed from the header row of a CSV file.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct HeaderData<T> where T : new()
{
    internal readonly ReflectionData<T>?[] typeInfo;
    internal readonly char sep;
    internal readonly bool handleSpeechMarks;
    internal int lineNo;

    internal readonly CSVParser<T>.RowWorker[]? rowWorkers;
    internal readonly CircularBuffer<CSVParser<T>.WorkItem>? workQueue;
    internal readonly ConcurrentBag<char[]>? charBuffers;
    internal int lineNoStart;

    internal HeaderData(ReflectionData<T>?[] typeInfo, char sep, bool handleSpeechMarks, int lineNo, 
        CSVParser<T>.RowWorker[]? rowWorkers = null, CircularBuffer<CSVParser<T>.WorkItem>? workQueue = null, ConcurrentBag<char[]>? charBuffers = null)
    {
        this.typeInfo = typeInfo;
        this.sep = sep;
        this.handleSpeechMarks = handleSpeechMarks;
        this.lineNo = lineNo;
        this.lineNoStart = lineNo;
        this.rowWorkers = rowWorkers;
        this.workQueue = workQueue;
        this.charBuffers = charBuffers;
    }

    public readonly IEnumerable<string> CSVColumnNames => typeInfo.Where(x => x.HasValue).Select(x => x!.Value.csvName ?? x.Value.fieldName);
}
