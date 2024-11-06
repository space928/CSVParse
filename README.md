# CSVParse
### *The Ultra Fast .NET CSV Parser*

[![Nuget](https://img.shields.io/nuget/v/CSVParse?logo=Nuget&logoColor=fff)](https://www.nuget.org/packages/CSVParse/)

CSVParse is a minimal, pure C# CSV file parser. It implements the vast majority 
of [RFC 4180](https://datatracker.ietf.org/doc/html/rfc4180). It's designed with 
the key goals of being fast, and having a straight-forward API.

### Features
 - Supports any separator character (allowing it to support CSV, TSV, and similar formats).
 - Parses data with zero GC allocations.
 - Supports parsing (or ignoring) CSV headers.
 - Supports fields in quotation marks, which can be automatically unescaped, as per the 
   RFC 4180 spec.
 - The CSV parser instance can be resued if multiple CSV files need to be parsed. (Doing 
   so saves the cost of constructing a new parser instance and doing reflection)
 - Custom deserializers can be written to parse fields into arbitrary data types.
 - Supports Windows/Unix/Mac line endings.
 - Automatic file encoding detection for UTF-8, UTF-16-LE, and UTF-16-BE.
 - Runtime IL generation to optimise deserializing into arbitrary classes/structs with 
   minimal use of reflection.
 - Multithreading

### Important Limitations
 - The RFC 4180 specification allows for line breaks inside quoted fields, due to 
   the internal design of CSVParse, this is not supported.
 - Currently on platforms that do not support runtime IL generation (this includes 
   NativeAOT), a much slower reflection based fallback path is used. This may be 
   circumvented in the future with the use of compile-time source generation.
 - To reduce the number of memory allocations, CSVParse currently uses a fixed size 
   line buffer. This means that lines in the CSV file cannot be longer than the 
   configured maximum for the parser. This limit is configurable and may be lifted 
   in the future.

### Benchmarks

See `CSVParse.Benchmarks/CSVParseBenchmarks.cs` for details.
```c
// * Summary *

BenchmarkDotNet v0.13.12, Windows 11 (10.0.22631.3880/23H2/2023Update/SunValley3)
Intel Core i5-8300H CPU 2.30GHz (Coffee Lake), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.303
  [Host]     : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2
  Job-SZBXTU : .NET 8.0.7 (8.0.724.31311), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=20  WarmupCount=4

| Method                            | Mean     | Error    | StdDev   | Gen0      | Gen1      | Gen2     | Allocated   |
|---------------------------------- |---------:|---------:|---------:|----------:|----------:|---------:|------------:|
| TestCSVParseClass                 | 45.59 ms | 0.792 ms | 0.813 ms | 1818.1818 | 1181.8182 | 363.6364 |  9703.92 KB |
| TestCSVParseStruct                | 42.48 ms | 1.435 ms | 1.652 ms | 1250.0000 |  750.0000 | 250.0000 |  10432.3 KB |
| TestCSVParseStructNoIt            | 38.94 ms | 0.759 ms | 0.874 ms | 1230.7692 |  769.2308 | 230.7692 |  7898.36 KB |
| TestCSVParseStructNoItNoCustomSer | 31.07 ms | 1.034 ms | 1.149 ms |  750.0000 |  437.5000 | 125.0000 |  5554.23 KB |
| TestCSVParseStructNoItNoAlloc     | 23.59 ms | 0.604 ms | 0.695 ms |         - |         - |        - |    15.31 KB |
| TestCSVParseOldClass              | 47.36 ms | 1.169 ms | 1.346 ms | 1800.0000 |  900.0000 | 300.0000 | 10057.61 KB |
| TestCSVHelper                     | 71.52 ms | 2.225 ms | 2.563 ms | 3000.0000 | 1333.3333 | 333.3333 | 17964.23 KB |
```

### Installation

Simply add the NuGet package to your project.
```
> dotnet add package CSVParse
```

### Usage

The simplest way to use CSVParse is to call that static `Parse<>()` method.
```cs
using CSVParse;

// Define a data structure to representing a row of data
public struct Row
{
	public string name;
	public int id;
	public float time;
}

// Open a file to parse, this can be replaced with any C# stream
string path = @"C:\path\to\your\csv.txt";
using FileStream fs = File.OpenRead(path);

// Call the static parse method
var csv = CSVParser.Parse<Row>(fs);

// Now you can process the rows as needed
foreach (var row in csv)
	Console.WriteLine($"{row.id}: {row.name} [time: {row.time}]");
```

A number of parsing options can be passed to CSVParse to control how CSV files are parsed:
```cs
string path = @"C:\path\to\your\csv.txt";
using FileStream fs = File.OpenRead(path);

// Specify some options, any of these can be left blank to use the default value.
var options = new CSVSerializerOptions()
{
    IncludeFields = true,
    IncludeProperties = true,
    IncludePrivate = false,
    HandleSpeechMarks = true
    Separator = ',',
    MaximumLineSize = 2048,
    HeaderMode = CSVHeaderMode.Parse,
    DefaultEncoding = null
};

// Call the static parse method
var csv = CSVParser.Parse<Row>(fs, options);
```

CSVParse provides a few attributes which can be applied to the fields of the row data 
structure to control how they are parsed:
```cs
// The [CSVName(...)] Attribute can be applied to fields where the field name miht not 
// match the CSV column name. In ths example, in the CSV header, the column named "trip_id" 
// will be matched to the field "tripID".
public readonly struct GTFSStopTimeStruct
{
    [CSVName("trip_id")]
    public readonly string tripID;
    [CSVName("arrival_time")]
    public readonly TransitTime arrivalTime;
    [CSVName("departure_time")]
    public readonly TransitTime departureTime;
    [CSVName("stop_id")]
    public readonly string stopID;
    [CSVName("shape_dist_traveled")]
    public readonly float? shapeDistTraveled;
}

// If your CSV file doesn't have a header (or you want to skip parsing it), then you can use
// the [CSVIndex(...)] attribute to control which column is associated with which field by 
// column index. This also makes it possible to skip parsing certain fields in the CSV file 
// if they aren't needed.
public readonly struct GTFSStopTimeStruct
{
    [CSVIndex(0)]
    public readonly string tripID;
    [CSVIndex(1)]
    public readonly TransitTime arrivalTime;
    [CSVIndex(2)]
    public readonly TransitTime departureTime;
    [CSVIndex(3)]
    public readonly string stopID;
    [CSVIndex(8)]
    public readonly float? shapeDistTraveled;
}

// To be able to deserialize custom data types you can apply the [CSVCustomSerializer<>]
// attribute to a field to specify a custom parser for that field. This attribute takes a 
// generic type argument which implements ICustomCSVSerializer.
public readonly struct GTFSStopTimeStruct
{
    [CSVName("trip_id")]
    public readonly string tripID;
    [CSVCustomSerializer<TransitTimeCSVSerializer>]
    [CSVName("arrival_time")]
    public readonly TransitTime arrivalTime;
    [CSVCustomSerializer<TransitTimeCSVSerializer>]
    [CSVName("departure_time")]
    public readonly TransitTime departureTime;
    [CSVName("stop_id")]
    public readonly string stopID;
    [CSVName("shape_dist_traveled")]
    public readonly float? shapeDistTraveled;
}
```

Custom field parsers can be implemented as shown in the following example. Note 
that there are two methods of implementing custom parses. Either using the 
`[CSVCustomSerializer]` attribute on a field an implementing a custom serializer (see 
`TransitTimeCSVSerializer`); or by simply implementing a constructor on the type which 
takes a single `ReadOnlySpan<char>` as a parameter and implementing the 
`int Serialize(Span<char> dst)` method.
```cs
//////// From CSVParser.cs
public interface ICustomCSVSerializer
{
    public object? Deserialize(ReadOnlySpan<char> data, int lineNumber);
    public ReadOnlySpan<char> Serialize(object? data, int lineNumber) => data?.ToString();
}
////////

public readonly struct GTFSStopTimeStruct
{
    [CSVName("trip_id")]
    public readonly string tripID;

    [CSVCustomSerializer<TransitTimeCSVSerializer>]
    [CSVName("arrival_time")]
    public readonly TransitTime arrivalTime;
}

public class TransitTimeCSVSerializer : ICustomCSVSerializer
{
    public object? Deserialize(ReadOnlySpan<char> data, int lineNumber)
    {
        return new TransitTime(data);
    }
}

public readonly struct TransitTime : ICSVSerializable
{
    public readonly int time;

    public TransitTime(ReadOnlySpan<char> s)
    {
        int hour = int.Parse(s[..2]);
        int min = int.Parse(s[3..5]);
        int second = int.Parse(s[6..8]);
        time = hour * 3600 + min * 60 + second;
    }

    public TransitTime(int seconds)
    {
        this.time = seconds;
    }

    public override string ToString()
    {
        var h = (time / 3600); // = 25
        var m = (time / 60 - (h * 60)); // = 30
        var s = time % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    public int Serialize(Span<char> dst)
    {
        // Note that this implementation make an unnecessary string allocation...
        var str = ToString();
        str.CopyTo(dst);
        return str.Length;
    }
}
```

#### Performance Considerations
CSVParse uses reflection to work out which fields in the CSV map to which 
fields in the row data structure. To avoid unnecessary copies and allocations we can store 
an instance of the CSVParser to be reused with other CSV files of the same type:

```cs
string path = @"C:\path\to\your\csv.txt";
using FileStream fs = File.OpenRead(path);

// Create an instance of the parser, this can be reused later if needed.
var parser = new CSVParser<Row>(options);

// Call the parse method
var csv = parser.Parse(fs);
```

If you intend on processing CSV data one line at a time, more allocations can be avoided
by reusing a single row object for each row in the CSV:
```cs
string path = @"C:\path\to\your\csv.txt";
using FileStream fs = File.OpenRead(path);

// Create an instance of the parser, this can be reused later if needed.
var parser = new CSVParser<Row>(options);

// Initialise the parser and read the header
var header = parser.Initialize(fs);
var row = new Row();
while (parser.ParseRow(ref header, fs, ref row))
{
    // Do something with the parsed row
}
```

Additionally, when processing one line at a time, string allocations can be avoided by making 
your data types use pre-allocated mutable strings. The `PreAllocatedString` helper struct is 
effectively a `Memory<char>`, and can be used to represent strings.
```cs
public readonly struct Row
{
    public Row() : this(256) { }

    public Row(int preallocateStringSize)
    {
        name = new PreAllocatedString(preallocateStringSize);
    }

	public readonly PreAllocatedString name;
	public readonly int id;
	public readonly float time;
}

string path = @"C:\path\to\your\csv.txt";
using FileStream fs = File.OpenRead(path);

// Create an instance of the parser, this can be reused later if needed.
var parser = new CSVParser<Row>(options);

// Initialise the parser and read the header
var header = parser.Initialize(fs);
var row = new Row(1024);
while (parser.ParseRow(ref header, fs, ref row))
{
    // Do something with the parsed row
}
```

### Bugs? Feature Requests?

Feel free to open a GitHub issue ;-)

### License

This library is available under the very permissive MIT license.

### Acknowledgements

Adam and Derek Mathieson for help finding bugs.
