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

    internal HeaderData(ReflectionData<T>?[] typeInfo, char sep, bool handleSpeechMarks, int lineNo)
    {
        this.typeInfo = typeInfo;
        this.sep = sep;
        this.handleSpeechMarks = handleSpeechMarks;
        this.lineNo = lineNo;
    }

    public readonly IEnumerable<string> CSVColumnNames => typeInfo.Where(x => x.HasValue).Select(x => x!.Value.csvName ?? x.Value.fieldName);
}
