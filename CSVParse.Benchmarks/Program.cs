using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using CsvHelper;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CSVParse.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Running benchmarks...");
        string path = @"C:\Users\Thoma\Downloads\stop_times.txt";
        if (!File.Exists(path))
        {
            TestDataGenerator.GenerateTestData(path, 20_000_000);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        }

#if DEBUG
        TestLargeFile(path);
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

    private static void TestLargeFile(string path)
    {
        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ','
        };
        var parser = new CSVParser<GTFSStopTimeStruct>(options);
        var parserNoCusSer = new CSVParser<GTFSStopTimeStructNoCustomSer>(options);
        var parserNoAlloc = new CSVParser<GTFSStopTimeStructFast>(options);
        var parserOld = new CSVParserOld<GTFSStopTimeStruct>(options);

        int rowsToParse = 1_000_0000;

#if true
        /*{
            using FileStream fs = File.OpenRead(path);
            using var reader = new StreamReader(fs);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<GTFSStopTime>().Take(rowsToParse).ToList();
        }*/

        {
            Stopwatch sw = new();
            sw.Start();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(path);

            var header = parserNoCusSer.Initialise(fs);
            var csv = new GTFSStopTimeStructNoCustomSer[rowsToParse];
            for (int i = 0; i < csv.Length; i++)
                parserNoCusSer.ParseRow(ref header, fs, ref csv[i]);
            sw.Stop();
            Console.WriteLine($"New! Loaded {csv.Length} records in {sw.Elapsed}!");
        }

        {
            Stopwatch sw = new();
            sw.Start();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(path);

            var header = parserNoAlloc.Initialise(fs);
            var csv = new GTFSStopTimeStructFast(1024);
            for (int i = 0; i < rowsToParse; i++)
                parserNoAlloc.ParseRow(ref header, fs, ref csv);
            sw.Stop();
            Console.WriteLine($"New (No alloc)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }

#else
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
#endif
    }
}
