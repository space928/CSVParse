using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace CSVParse;

internal class Program
{
    static void Main(string[] args)
    {
        //Tmp();
        Console.WriteLine("Running benchmarks...");
        //TestCSVParser();
        //LineReadingTests(); 
        Console.WriteLine("Done");
        Console.ReadLine();
    }
}

/*

Conclusions:
Supporting multiple line endings doesn't add much cost.
ReadLines is fast enough in most cases
IniFileReader and FastReader are similar, the latter having a slight edge and both avoid allocations
They are better for larger files, they also have a slight edge for shorter lines
Vectorisation is important. FastReader uses a vectorised FindIndex but a non vectorised Ascii->UTF16 conversion.
The original FastUnsafeReader used no vectorisation, hence why it ran so much slower than FastReader


| Method               | FileName             | Mean      | Error     | StdDev    | Ratio    | RatioSD | Gen0       | Gen1       | Gen2      | Allocated | Alloc Ratio |
|--------------------- |--------------------- |----------:|----------:|----------:|---------:|--------:|-----------:|-----------:|----------:|----------:|------------:|
| TestReadLines        | D:\\T(...).txt [130] |  41.04 ms |  0.793 ms |  0.524 ms | baseline |         |  5833.3333 |  2166.6667 | 1000.0000 |  33.78 MB |             |
| TestOldIniFileReader | D:\\T(...).txt [130] |  33.31 ms |  0.447 ms |  0.295 ms |     -19% |    1.6% |  1400.0000 |  1266.6667 |  666.6667 |   8.61 MB |        -75% |
| TestIniFileReader    | D:\\T(...).txt [130] |  32.21 ms |  0.434 ms |  0.287 ms |     -22% |    1.4% |  1375.0000 |  1250.0000 |  625.0000 |   8.61 MB |        -75% |
| TestFastReader       | D:\\T(...).txt [130] |  25.80 ms |  0.298 ms |  0.197 ms |     -37% |    1.6% |  1437.5000 |  1375.0000 |  687.5000 |   8.69 MB |        -74% |
| TestFastUTF8Reader   | D:\\T(...).txt [130] |  33.95 ms |  0.458 ms |  0.303 ms |     -17% |    1.6% |  1333.3333 |  1200.0000 |  600.0000 |    8.6 MB |        -75% |
| TestFastUTF8Reader1  | D:\\T(...).txt [130] |  29.10 ms |  0.240 ms |  0.159 ms |     -29% |    1.3% |  1437.5000 |  1281.2500 |  656.2500 |    8.6 MB |        -75% |
| TestFastVectorReader | D:\\T(...).txt [130] |  23.39 ms |  0.223 ms |  0.148 ms |     -43% |    1.5% |  1437.5000 |  1406.2500 |  718.7500 |   8.69 MB |        -74% |
|                      |                      |           |           |           |          |         |            |            |           |           |             |
| TestReadLines        | D:\\T(...).txt [129] | 219.93 ms |  8.969 ms |  5.337 ms | baseline |         | 31000.0000 | 12000.0000 | 3000.0000 |  246.1 MB |             |
| TestOldIniFileReader | D:\\T(...).txt [129] | 225.05 ms |  8.250 ms |  5.457 ms |      +3% |    3.4% | 14333.3333 |  8000.0000 | 2000.0000 | 148.32 MB |        -40% |
| TestIniFileReader    | D:\\T(...).txt [129] | 142.11 ms | 15.409 ms | 10.192 ms |     -36% |    7.5% | 14500.0000 |  8250.0000 | 2250.0000 | 148.32 MB |        -40% |
| TestFastReader       | D:\\T(...).txt [129] | 145.36 ms |  7.752 ms |  4.613 ms |     -34% |    3.8% | 14750.0000 |  8500.0000 | 2500.0000 | 149.84 MB |        -39% |
| TestFastUTF8Reader   | D:\\T(...).txt [129] | 150.21 ms | 11.486 ms |  7.597 ms |     -31% |    4.7% | 14250.0000 |  8000.0000 | 2250.0000 | 148.31 MB |        -40% |
| TestFastUTF8Reader1  | D:\\T(...).txt [129] | 170.03 ms |  9.272 ms |  5.517 ms |     -23% |    3.2% | 14500.0000 |  8250.0000 | 2250.0000 | 148.31 MB |        -40% |
| TestFastVectorReader | D:\\T(...).txt [129] | 129.56 ms | 12.183 ms |  8.059 ms |     -41% |    7.3% | 14750.0000 |  8500.0000 | 2500.0000 | 149.84 MB |        -39% |


// * Summary *

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.4651/22H2/2022Update)
Intel Core i5-6600 CPU 3.30GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.300
  [Host]     : .NET 8.0.5 (8.0.524.21615), X64 AOT AVX2
  Job-GFKCJD : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=10  WarmupCount=4

| Method               | FileName             | Mean         | Error       | StdDev      | Ratio | RatioSD |
|--------------------- |--------------------- |-------------:|------------:|------------:|------:|--------:|
| TestReadLines        | 4shor(...)g.txt [23] |  20,589.5 us |   456.56 us |   301.98 us |  1.00 |    0.00 |
| TestFastReader       | 4shor(...)g.txt [23] |  12,209.1 us |   143.88 us |    95.17 us |  0.59 |    0.01 |
| TestFastVectorReader | 4shor(...)g.txt [23] |  11,017.0 us |   119.90 us |    79.30 us |  0.54 |    0.01 |
|                      |                      |              |             |             |       |         |
| TestReadLines        | 6long(...)t.txt [23] |     114.8 us |     1.47 us |     0.87 us |  1.00 |    0.00 |
| TestFastReader       | 6long(...)t.txt [23] |     109.2 us |     0.92 us |     0.55 us |  0.95 |    0.01 |
| TestFastVectorReader | 6long(...)t.txt [23] |     106.1 us |     1.87 us |     1.24 us |  0.92 |    0.01 |
|                      |                      |              |             |             |       |         |
| TestReadLines        | 8long(...)g.txt [22] | 103,315.2 us | 3,390.79 us | 2,242.80 us |  1.00 |    0.00 |
| TestFastReader       | 8long(...)g.txt [22] |  77,367.1 us | 4,342.59 us | 2,872.36 us |  0.75 |    0.02 |
| TestFastVectorReader | 8long(...)g.txt [22] |  63,365.1 us | 7,480.19 us | 3,912.28 us |  0.62 |    0.05 |


// * Summary *

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.4651/22H2/2022Update)
Intel Core i5-6600 CPU 3.30GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.300
  [Host]     : .NET 8.0.5 (8.0.524.21615), X64 AOT AVX2 [AttachedDebugger]
  Job-HWVZPU : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2

Runtime=.NET 8.0  IterationCount=10  WarmupCount=4

| Method                     | FileName             | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated | Alloc Ratio |
|--------------------------- |--------------------- |---------:|---------:|---------:|------:|--------:|----------:|----------:|----------:|----------:|------------:|
| TestReadLines              | 8long(...)g.txt [22] | 14.27 ms | 0.402 ms | 0.239 ms |  1.00 |    0.00 | 3093.7500 | 2062.5000 | 1000.0000 |   17.9 MB |        1.00 |
| TestReadAllLines           | 8long(...)g.txt [22] | 26.78 ms | 1.371 ms | 0.816 ms |  1.88 |    0.07 | 3125.0000 | 2968.7500 | 1093.7500 |  20.66 MB |        1.15 |
| TestIniFileReader          | 8long(...)g.txt [22] | 15.89 ms | 0.415 ms | 0.274 ms |  1.12 |    0.03 | 1718.7500 | 1640.6250 |  812.5000 |  10.38 MB |        0.58 |
| TestFastReader             | 8long(...)g.txt [22] | 11.66 ms | 0.507 ms | 0.335 ms |  0.82 |    0.03 | 1734.3750 | 1703.1250 |  843.7500 |  10.45 MB |        0.58 |
| TestFastUnsafeReader       | 8long(...)g.txt [22] | 15.04 ms | 0.248 ms | 0.148 ms |  1.05 |    0.02 | 1734.3750 | 1718.7500 |  859.3750 |  10.43 MB |        0.58 |
| TestFastMemoryMappedReader | 8long(...)g.txt [22] | 13.42 ms | 0.164 ms | 0.108 ms |  0.94 |    0.02 | 1734.3750 | 1484.3750 |  750.0000 |  10.44 MB |        0.58 |
| TestFastPipelinedReader    | 8long(...)g.txt [22] | 19.21 ms | 1.530 ms | 1.012 ms |  1.35 |    0.08 | 2218.7500 | 1937.5000 |  968.7500 |  12.75 MB |        0.71 |


// * Summary *

BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.4651/22H2/2022Update)
Intel Core i5-6600 CPU 3.30GHz (Skylake), 1 CPU, 4 logical and 4 physical cores
.NET SDK 8.0.300
  [Host]     : .NET 8.0.5 (8.0.524.21615), X64 AOT AVX2 [AttachedDebugger]
  Job-VRMQZT : .NET 8.0.5 (8.0.524.21615), X64 RyuJIT AVX2
  Job-TAUSUX : .NET 8.0.5, X64 NativeAOT AVX2

IterationCount=10  WarmupCount=4

| Method            | Runtime       | FileName             | Mean        | Error       | StdDev      | Ratio | RatioSD | Gen0      | Gen1      | Gen2      | Allocated   | Alloc Ratio |
|------------------ |-------------- |--------------------- |------------:|------------:|------------:|------:|--------:|----------:|----------:|----------:|------------:|------------:|
| TestReadLines     | .NET 8.0      | 1shor(...)t.txt [23] |    105.7 us |     0.60 us |     0.36 us |  1.00 |    0.01 |    3.4180 |         - |         - |    10.67 KB |        0.89 |
| TestReadAllLines  | .NET 8.0      | 1shor(...)t.txt [23] |    105.2 us |     1.06 us |     0.70 us |  1.00 |    0.00 |    3.7842 |         - |         - |    11.96 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 1shor(...)t.txt [23] |    105.1 us |     0.98 us |     0.65 us |  1.00 |    0.01 |    4.5166 |         - |         - |    13.95 KB |        1.17 |
| TestFastReader    | .NET 8.0      | 1shor(...)t.txt [23] |    102.9 us |     0.70 us |     0.47 us |  0.98 |    0.01 |    1.8311 |         - |         - |     5.95 KB |        0.50 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 1shor(...)t.txt [23] |    109.4 us |     0.88 us |     0.58 us |  1.00 |    0.01 |    3.4180 |         - |         - |    10.54 KB |        0.86 |
| TestReadAllLines  | NativeAOT 8.0 | 1shor(...)t.txt [23] |    109.8 us |     0.63 us |     0.37 us |  1.00 |    0.00 |    3.9063 |         - |         - |    12.22 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 1shor(...)t.txt [23] |    108.3 us |     1.92 us |     1.27 us |  0.99 |    0.01 |    4.3945 |         - |         - |    13.84 KB |        1.13 |
| TestFastReader    | NativeAOT 8.0 | 1shor(...)t.txt [23] |    106.0 us |     0.72 us |     0.48 us |  0.97 |    0.01 |    1.8311 |         - |         - |     5.85 KB |        0.48 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 2shor(...)t.txt [24] |    106.5 us |     0.84 us |     0.50 us |  0.99 |    0.01 |    3.5400 |         - |         - |    10.92 KB |        0.89 |
| TestReadAllLines  | .NET 8.0      | 2shor(...)t.txt [24] |    107.2 us |     0.64 us |     0.42 us |  1.00 |    0.00 |    3.9063 |         - |         - |    12.27 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 2shor(...)t.txt [24] |    106.1 us |     0.66 us |     0.35 us |  0.99 |    0.00 |    4.5166 |         - |         - |    13.97 KB |        1.14 |
| TestFastReader    | .NET 8.0      | 2shor(...)t.txt [24] |    104.3 us |     0.35 us |     0.23 us |  0.97 |    0.00 |    1.8311 |         - |         - |     5.86 KB |        0.48 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 2shor(...)t.txt [24] |    109.8 us |     0.71 us |     0.42 us |  1.00 |    0.01 |    3.4180 |         - |         - |    10.59 KB |        0.87 |
| TestReadAllLines  | NativeAOT 8.0 | 2shor(...)t.txt [24] |    109.8 us |     0.72 us |     0.43 us |  1.00 |    0.00 |    3.9063 |         - |         - |     12.2 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 2shor(...)t.txt [24] |    108.4 us |     0.62 us |     0.41 us |  0.99 |    0.00 |    4.5166 |         - |         - |    13.85 KB |        1.14 |
| TestFastReader    | NativeAOT 8.0 | 2shor(...)t.txt [24] |    105.5 us |     0.68 us |     0.40 us |  0.96 |    0.00 |    1.8311 |         - |         - |     5.74 KB |        0.47 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 3shor(...)g.txt [22] |  4,437.5 us |    23.55 us |    14.01 us |  0.61 |    0.01 |  679.6875 |  312.5000 |  140.6250 |  3480.48 KB |        0.55 |
| TestReadAllLines  | .NET 8.0      | 3shor(...)g.txt [22] |  7,247.7 us |   154.30 us |   102.06 us |  1.00 |    0.00 |  984.3750 |  984.3750 |  984.3750 |  6312.11 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 3shor(...)g.txt [22] |  3,731.5 us |    39.80 us |    26.32 us |  0.51 |    0.01 |  121.0938 |  121.0938 |  121.0938 |   907.53 KB |        0.14 |
| TestFastReader    | .NET 8.0      | 3shor(...)g.txt [22] |  3,066.7 us |    44.06 us |    26.22 us |  0.42 |    0.01 |  199.2188 |  199.2188 |  199.2188 |  1288.31 KB |        0.20 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 3shor(...)g.txt [22] |  4,888.3 us |    59.08 us |    39.08 us |  0.62 |    0.01 |  703.1250 |  289.0625 |  140.6250 |  3481.44 KB |        0.55 |
| TestReadAllLines  | NativeAOT 8.0 | 3shor(...)g.txt [22] |  7,874.0 us |   125.56 us |    83.05 us |  1.00 |    0.00 |  984.3750 |  984.3750 |  984.3750 |  6285.58 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 3shor(...)g.txt [22] |  3,741.6 us |    27.95 us |    18.49 us |  0.48 |    0.01 |  121.0938 |  121.0938 |  121.0938 |   891.07 KB |        0.14 |
| TestFastReader    | NativeAOT 8.0 | 3shor(...)g.txt [22] |  3,153.9 us |    40.13 us |    26.54 us |  0.40 |    0.01 |  199.2188 |  199.2188 |  199.2188 |  1286.11 KB |        0.20 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 4shor(...)g.txt [23] |  4,156.5 us |    52.95 us |    35.02 us |  0.59 |    0.01 |  718.7500 |  367.1875 |  117.1875 |  3467.92 KB |        0.55 |
| TestReadAllLines  | .NET 8.0      | 4shor(...)g.txt [23] |  7,027.9 us |    69.21 us |    45.78 us |  1.00 |    0.00 |  992.1875 |  992.1875 |  992.1875 |  6314.38 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 4shor(...)g.txt [23] |  3,439.7 us |    29.82 us |    15.60 us |  0.49 |    0.00 |  121.0938 |  121.0938 |  121.0938 |    907.4 KB |        0.14 |
| TestFastReader    | .NET 8.0      | 4shor(...)g.txt [23] |  2,736.9 us |    25.20 us |    16.67 us |  0.39 |    0.00 |  140.6250 |  140.6250 |  140.6250 |   903.63 KB |        0.14 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 4shor(...)g.txt [23] |  4,574.5 us |    50.20 us |    33.20 us |  0.60 |    0.01 |  718.7500 |  359.3750 |  117.1875 |     3463 KB |        0.55 |
| TestReadAllLines  | NativeAOT 8.0 | 4shor(...)g.txt [23] |  7,693.8 us |    76.53 us |    40.03 us |  1.00 |    0.00 |  992.1875 |  992.1875 |  992.1875 |  6307.51 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 4shor(...)g.txt [23] |  3,621.6 us |    38.62 us |    25.55 us |  0.47 |    0.00 |  121.0938 |  121.0938 |  121.0938 |   907.79 KB |        0.14 |
| TestFastReader    | NativeAOT 8.0 | 4shor(...)g.txt [23] |  2,788.3 us |    21.86 us |    11.43 us |  0.36 |    0.00 |  140.6250 |  140.6250 |  140.6250 |    902.7 KB |        0.14 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 5long(...)t.txt [22] |    115.8 us |     1.08 us |     0.64 us |  0.99 |    0.01 |    6.8359 |         - |         - |     21.1 KB |        0.89 |
| TestReadAllLines  | .NET 8.0      | 5long(...)t.txt [22] |    117.6 us |     2.04 us |     1.35 us |  1.00 |    0.00 |    7.5684 |         - |         - |    23.81 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 5long(...)t.txt [22] |    114.6 us |     1.32 us |     0.78 us |  0.98 |    0.01 |    7.6904 |         - |         - |    23.77 KB |        1.00 |
| TestFastReader    | .NET 8.0      | 5long(...)t.txt [22] |    116.5 us |     3.13 us |     2.07 us |  0.99 |    0.02 |    4.3945 |         - |         - |    13.63 KB |        0.57 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 5long(...)t.txt [22] |    120.8 us |     6.50 us |     4.30 us |  1.01 |    0.04 |    7.0801 |         - |         - |    21.71 KB |        0.99 |
| TestReadAllLines  | NativeAOT 8.0 | 5long(...)t.txt [22] |    119.8 us |     4.60 us |     3.05 us |  1.00 |    0.00 |    7.0801 |         - |         - |    21.83 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 5long(...)t.txt [22] |    116.0 us |     5.36 us |     3.55 us |  0.97 |    0.03 |    6.7139 |         - |         - |    20.76 KB |        0.95 |
| TestFastReader    | NativeAOT 8.0 | 5long(...)t.txt [22] |    107.7 us |     0.77 us |     0.40 us |  0.90 |    0.03 |    4.5166 |         - |         - |    14.05 KB |        0.64 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 6long(...)t.txt [23] |    119.2 us |     6.82 us |     4.51 us |  1.01 |    0.06 |    6.5918 |         - |         - |    20.75 KB |        0.94 |
| TestReadAllLines  | .NET 8.0      | 6long(...)t.txt [23] |    118.1 us |     5.45 us |     3.60 us |  1.00 |    0.00 |    7.0801 |         - |         - |    22.13 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 6long(...)t.txt [23] |    114.4 us |     2.85 us |     1.49 us |  0.98 |    0.03 |    6.8359 |         - |         - |    21.31 KB |        0.96 |
| TestFastReader    | .NET 8.0      | 6long(...)t.txt [23] |    110.3 us |     0.60 us |     0.40 us |  0.94 |    0.03 |    4.5166 |         - |         - |    14.02 KB |        0.63 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 6long(...)t.txt [23] |    116.4 us |     1.42 us |     0.94 us |  1.00 |    0.01 |    6.5918 |         - |         - |    20.27 KB |        0.92 |
| TestReadAllLines  | NativeAOT 8.0 | 6long(...)t.txt [23] |    116.7 us |     1.66 us |     1.10 us |  1.00 |    0.00 |    7.0801 |         - |         - |    22.03 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 6long(...)t.txt [23] |    113.8 us |     0.74 us |     0.49 us |  0.97 |    0.01 |    7.0801 |         - |         - |    22.07 KB |        1.00 |
| TestFastReader    | NativeAOT 8.0 | 6long(...)t.txt [23] |    108.6 us |     1.12 us |     0.74 us |  0.93 |    0.01 |    4.0283 |         - |         - |    12.55 KB |        0.57 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 7long(...)g.txt [21] | 15,260.7 us | 1,312.17 us |   867.92 us |  0.57 |    0.04 | 3109.3750 | 2125.0000 | 1015.6250 |  18308.1 KB |        0.86 |
| TestReadAllLines  | .NET 8.0      | 7long(...)g.txt [21] | 26,897.4 us | 2,216.62 us | 1,466.16 us |  1.00 |    0.00 | 3093.7500 | 2937.5000 | 1031.2500 | 21178.34 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 7long(...)g.txt [21] | 15,908.3 us |   146.42 us |    96.85 us |  0.59 |    0.03 | 1718.7500 | 1625.0000 |  812.5000 | 10608.45 KB |        0.50 |
| TestFastReader    | .NET 8.0      | 7long(...)g.txt [21] | 11,823.3 us |   139.65 us |    83.10 us |  0.44 |    0.02 | 1796.8750 | 1765.6250 |  875.0000 | 11113.69 KB |        0.52 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 7long(...)g.txt [21] | 17,240.3 us |   962.14 us |   636.40 us |  0.58 |    0.02 | 3093.7500 | 2093.7500 | 1031.2500 | 18289.52 KB |        0.86 |
| TestReadAllLines  | NativeAOT 8.0 | 7long(...)g.txt [21] | 29,645.2 us |   919.24 us |   608.02 us |  1.00 |    0.00 | 3125.0000 | 2906.2500 | 1093.7500 | 21178.53 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 7long(...)g.txt [21] | 15,211.8 us |   179.28 us |   118.58 us |  0.51 |    0.01 | 1718.7500 | 1625.0000 |  796.8750 | 10614.53 KB |        0.50 |
| TestFastReader    | NativeAOT 8.0 | 7long(...)g.txt [21] | 11,723.9 us |   215.41 us |   142.48 us |  0.40 |    0.01 | 1734.3750 | 1640.6250 |  796.8750 | 11118.15 KB |        0.52 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | .NET 8.0      | 8long(...)g.txt [22] | 14,335.3 us |   380.71 us |   199.12 us |  0.53 |    0.02 | 3093.7500 | 2093.7500 | 1000.0000 | 18341.08 KB |        0.87 |
| TestReadAllLines  | .NET 8.0      | 8long(...)g.txt [22] | 27,014.9 us | 1,175.40 us |   699.46 us |  1.00 |    0.00 | 3125.0000 | 2937.5000 | 1062.5000 | 21167.61 KB |        1.00 |
| TestIniFileReader | .NET 8.0      | 8long(...)g.txt [22] | 15,777.4 us |   133.57 us |    88.35 us |  0.58 |    0.02 | 1718.7500 | 1593.7500 |  781.2500 | 10633.75 KB |        0.50 |
| TestFastReader    | .NET 8.0      | 8long(...)g.txt [22] | 11,338.9 us |   714.56 us |   472.64 us |  0.42 |    0.01 | 1734.3750 | 1687.5000 |  843.7500 | 10721.17 KB |        0.51 |
|                   |               |                      |             |             |             |       |         |           |           |           |             |             |
| TestReadLines     | NativeAOT 8.0 | 8long(...)g.txt [22] | 17,978.3 us | 1,033.45 us |   683.56 us |  0.61 |    0.03 | 3093.7500 | 2062.5000 | 1031.2500 | 18325.13 KB |        0.87 |
| TestReadAllLines  | NativeAOT 8.0 | 8long(...)g.txt [22] | 29,601.7 us |   843.75 us |   558.09 us |  1.00 |    0.00 | 3125.0000 | 2906.2500 | 1062.5000 | 21162.69 KB |        1.00 |
| TestIniFileReader | NativeAOT 8.0 | 8long(...)g.txt [22] | 15,256.8 us |   560.76 us |   370.91 us |  0.52 |    0.02 | 1703.1250 | 1687.5000 |  828.1250 | 10597.99 KB |        0.50 |
| TestFastReader    | NativeAOT 8.0 | 8long(...)g.txt [22] | 11,442.8 us |   258.67 us |   171.10 us |  0.39 |    0.01 | 1734.3750 | 1609.3750 |  812.5000 | 10709.26 KB |        0.51 |
 
 */
