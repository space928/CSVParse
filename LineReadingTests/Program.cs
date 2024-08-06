using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using CSVParse;
using System.Diagnostics;

namespace LineReadingTests;

internal class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Running benchmarks...");
        LineReadingTests(); 
        Console.WriteLine("Done");
        Console.ReadLine();
    }

    private static void LineReadingTests()
    {
#if false || DEBUG
        LineReaderBenchmarks benchmarks = new();
        benchmarks.Setup();
        benchmarks.FileName = "2shortLinesUnixShort.txt";//benchmarks.FileNames[0];
        benchmarks.FileName = "8longLinesUnixLong.txt";//benchmarks.FileNames[0];
                                                       //benchmarks.FileName = "9shortLinesUnixVeryLong.txt";//benchmarks.FileNames[0];
                                                       //BasicTests(benchmarks);

        //#if true
        BasicSpeedTests(benchmarks);
        //#else
        TestCorrectness(benchmarks);
        //#endif

#else
        var summary = BenchmarkRunner.Run(typeof(Program).Assembly, ManualConfig
            .Create(DefaultConfig.Instance)
            .WithSummaryStyle(
                DefaultConfig.Instance.SummaryStyle
                .WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Percentage))

            );
#endif
    }

    private static void BasicTests(LineReaderBenchmarks benchmarks)
    {
        Console.WriteLine("TestFastReader");
        Console.WriteLine(benchmarks.TestFastReader());
        Console.WriteLine("\n\n\nTestFastUTF8Reader");
        Console.WriteLine(benchmarks.TestFastUTF8Reader());
        Console.WriteLine("\n\n\nTestFastVectorReader");
        Console.WriteLine(benchmarks.TestFastVectorReader());
        Console.WriteLine("\n\n\nTestFastMemoryMappedReader");
        Console.WriteLine(benchmarks.TestFastMemoryMappedReader());
        Console.WriteLine("\n\n\nTestFastPipelinedReader");
        Console.WriteLine(benchmarks.TestFastPipelinedReader());
    }

    private static void BasicSpeedTests(LineReaderBenchmarks benchmarks)
    {
        /*Console.Write("Warmup.");
                var wstr = benchmarks.TestIniFileReader();
                Console.Write(".");
                wstr = benchmarks.TestOldIniFileReader();
                Console.Write(".");
                wstr = benchmarks.TestFastUTF8Reader();
                Console.Write(".");
                Console.WriteLine();*/
        Console.WriteLine("Testing...");
        //var str = benchmarks.TestFastUTF8Reader();
        Stopwatch sw = new();
        sw.Start();
        var str = benchmarks.TestIniFileReader();
        Console.WriteLine($"{sw.Elapsed} {str.Length}");
        Thread.Sleep(1000);
        sw.Restart();
        str = benchmarks.TestOldIniFileReader();
        Console.WriteLine($"{sw.Elapsed} {str.Length}");
        Thread.Sleep(1000);
        sw.Restart();
        str = benchmarks.TestFastUTF8Reader();
        Console.WriteLine($"{sw.Elapsed} {str.Length}");
    }

    private static void TestCorrectness(LineReaderBenchmarks benchmarks)
    {
        Console.WriteLine("Check correctness:");
        var goodLines = benchmarks.TestReadLines();
        var testLines = benchmarks.TestFastUTF8Reader();
        var same = goodLines.SequenceEqual(testLines);
        Console.WriteLine($"Same: {same}");
        if (!same)
            for (int i = 0; i < goodLines.Length; i++)
                if (goodLines[i] != testLines[i])
                {
                    Console.WriteLine($"@ [{i}] expected '{goodLines[i]}' got '{testLines[i]}'");
                    break;
                }
    }
}
