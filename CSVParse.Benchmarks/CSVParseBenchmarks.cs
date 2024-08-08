using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using CommandLine;
using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVParse.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80, launchCount: -1, warmupCount: 4, iterationCount: 20)]
[MemoryDiagnoser]
//[DryJob(RuntimeMoniker.Net80)]//RuntimeMoniker.Net80)]
//[DisassemblyDiagnoser(printSource:true, exportHtml:true, maxDepth:20)]
//[HardwareCounters(HardwareCounter.BranchInstructions, HardwareCounter.BranchMispredictions, HardwareCounter.LlcMisses)]
//[BenchmarkDotNet.Attributes.]
public class CSVParseBenchmarks
{
    public string Path { get; set; } = @"C:\Users\Thoma\Downloads\stop_times.txt";//@"D:\Thoma\Downloads\stop_times.txt";
    public int RowsToParse { get; set; } = 50_000;

    private MemoryStream? memoryStream;
    private readonly CSVParser<GTFSStopTime> parserClass;
    private readonly CSVParser<GTFSStopTimeStruct> parserStruct;
    private readonly CSVParser<GTFSStopTimeStructNoCustomSer> parserStructNoCustomSer;
    private readonly CSVParser<GTFSStopTimeStructFast> parserStructNoAlloc;
    private readonly CSVParserOld<GTFSStopTime> parserOldClass;
    private readonly CSVParserOld<GTFSStopTimeStruct> parserOldStruct;

    public CSVParseBenchmarks()
    {
        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ','
        };

        parserClass = new(options);
        parserStruct = new(options);
        parserStructNoCustomSer = new(options);
        parserStructNoAlloc = new(options);
        parserOldClass = new(options);
        parserOldStruct = new(options);
        //parserCsvHelper = new();
    }

    [GlobalSetup]
    public void GlobalSetup()
    {
        memoryStream = new(File.ReadAllBytes(Path));
    }

    [GlobalCleanup]
    public void GlobalCleanup() 
    {
        memoryStream?.Dispose();
    }

    [Benchmark]
    public void TestCSVParseClass()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var csv = parserClass.Parse(memoryStream).Take(RowsToParse).ToList();
        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVParseStruct()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var csv = parserStruct.Parse(memoryStream).Take(RowsToParse).ToList();
        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVParseStructNoIt()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;
        
        var header = parserStruct.Initialise(memoryStream);
        var csv = new GTFSStopTimeStruct[RowsToParse];
        for (int i = 0; i < csv.Length; i++)
            parserStruct.ParseRow(ref header, memoryStream, ref csv[i]);

        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVParseStructNoItNoCustomSer()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var header = parserStructNoCustomSer.Initialise(memoryStream);
        var csv = new GTFSStopTimeStructNoCustomSer[RowsToParse];
        for (int i = 0; i < csv.Length; i++)
            parserStructNoCustomSer.ParseRow(ref header, memoryStream, ref csv[i]);

        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVParseStructNoItNoAlloc()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var header = parserStructNoAlloc.Initialise(memoryStream);
        var csv = new GTFSStopTimeStructFast(1024);
        //int tmp = 0;
        for (int i = 0; i < RowsToParse; i++)
        {
            parserStructNoAlloc.ParseRow(ref header, memoryStream, ref csv);
            /*unchecked
            {
                tmp += csv.DepartureTime.time;
            }*/
        }

        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVParseOldClass()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var csv = parserOldClass.Parse(memoryStream).Take(RowsToParse).ToList();
        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    //[Benchmark]
    public void TestCSVParseOldStruct()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        var csv = parserOldStruct.Parse(memoryStream).Take(RowsToParse).ToList();
        //Console.WriteLine($"Loaded {csv.Count} records!");
    }

    [Benchmark]
    public void TestCSVHelper()
    {
        if (memoryStream == null)
            return;
        memoryStream.Position = 0;

        using var reader = new StreamReader(memoryStream, leaveOpen: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture, true);
        var records = csv.GetRecords<GTFSStopTime>().Take(RowsToParse).ToList();

        //var csv = parserOldClass.Parse(memoryStream).Take(RowsToParse).ToList();
        //Console.WriteLine($"Loaded {csv.Count} records!");
    }
}
