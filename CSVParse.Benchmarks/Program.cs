using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System.Diagnostics;

namespace CSVParse.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Running benchmarks...");
#if true || DEBUG
        TestLargeFile();
#else
        CSVParseBenchmarks();
#endif
        Console.WriteLine("Done");
        Console.ReadLine();
    }

    static void CSVParseBenchmarks()
    {
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly, ManualConfig
            .Create(DefaultConfig.Instance)
            .WithSummaryStyle(
                DefaultConfig.Instance.SummaryStyle
                .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Percentage))
            );
    }

    private static void TestLargeFile()
    {
        string path = @"D:\Thoma\Downloads\stop_times.txt";

        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ','
        };
        var parser = new CSVParser<GTFSStopTimeStruct>(options);
        var parserOld = new CSVParserOld<GTFSStopTimeStruct>(options);

        int rowsToParse = 300_0000;

        // Warmup
        {
            using FileStream fs = File.OpenRead(path);
            var csv = parser.Parse(fs).Take(rowsToParse).ToList();
            fs.Position = 0;
            var csv1 = parserOld.Parse(fs).Take(rowsToParse).ToList();
            Console.WriteLine($"Warmup! Loaded {csv.Count} and {csv1.Count} records!");
        }

        {
            Stopwatch sw = new();
            sw.Start();
            using FileStream fs = File.OpenRead(path);
            var csv = parser.Parse(fs).Take(rowsToParse).ToList();
            sw.Stop();
            Console.WriteLine($"New! Loaded {csv.Count} records in {sw.Elapsed}!");
        }

        Thread.Sleep(1500);

        {
            Stopwatch sw = new();
            sw.Start();
            using FileStream fs = File.OpenRead(path);
            var csv = parserOld.Parse(fs).Take(rowsToParse).ToList();
            sw.Stop();
            Console.WriteLine($"Old! Loaded {csv.Count} records in {sw.Elapsed}!");
        }
    }
}
