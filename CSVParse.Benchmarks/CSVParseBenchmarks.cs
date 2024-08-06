using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVParse.Benchmarks;

[SimpleJob(RuntimeMoniker.Net80, launchCount: -1, warmupCount: 4, iterationCount: 10)]
[MemoryDiagnoser]
//[DryJob(RuntimeMoniker.Net80)]//RuntimeMoniker.Net80)]
//[DisassemblyDiagnoser(printSource:true, exportHtml:true, maxDepth:20)]
[HardwareCounters(HardwareCounter.BranchInstructions, HardwareCounter.BranchMispredictions, HardwareCounter.LlcMisses)]
//[BenchmarkDotNet.Attributes.]
public class CSVParseBenchmarks
{
    public string Path { get; set; } = @"D:\Thoma\Downloads\stop_times.txt";
    public int RowsToParse { get; set; } = 50_000;

    private MemoryStream? memoryStream;
    private readonly CSVParser<GTFSStopTime> parserClass;
    private readonly CSVParser<GTFSStopTimeStruct> parserStruct;
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
        parserOldClass = new(options);
        parserOldStruct = new(options);
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

    //[Benchmark]
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
}
