using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;
using CSVParse.Benchmarks;

namespace CSVParse.Test;

internal class Program
{
    static void Main(string[] args)
    {
        //Console.ReadLine();
        Console.WriteLine("Testing...");
        string path = @"C:\Users\Thoma\Downloads\stop_times.txt";
        string pathq = @"C:\Users\Thoma\Downloads\stop_times_q.txt";

        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

        if (!File.Exists(path))
        {
            TestDataGenerator.GenerateTestData(path, 20_000_000, false);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        }

        if (!File.Exists(pathq))
        {
            TestDataGenerator.GenerateTestData(pathq, 20_000_000, true);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        }

        //TestCSVParse<Test>();
        //TestCSVParser();
        //TestLargeFile(path, pathq);
        TestMT(pathq);
        Console.WriteLine("DONE!");
        Console.ReadLine();
    }

    private static void TestMT(string pathq)
    {
        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ',',
            UseFastFloat = false,
        };
        var options2 = new CSVSerializerOptions(options)
        {
            HandleSpeechMarks = true
        };
        var options3 = new CSVSerializerOptions(options2)
        {
            Multithreaded = true,
            UseFastFloat = true,
        };
        var parserNoCusSer = new CSVParser<GTFSStopTimeStructNoCustomSer>(options);
        var parserNoAllocQuote = new CSVParser<GTFSStopTimeStructFast>(options2);
        var parserThreaded = new CSVParser<GTFSStopTimeStructNoCustomSer>(options3);
        var parserThreadedNoAlloc = new CSVParser<GTFSStopTimeStructFast>(options3);

        int rowsToParse = 5_000_000;

        //using MemoryStream ms = new(File.ReadAllBytes(pathq));

        {
            Stopwatch sw = new();
            sw.Start();
            var csv = new GTFSStopTimeStructFast(1024);
            var dst = new GTFSStopTimeStructFast[rowsToParse];
            for (int i = 0; i < rowsToParse; i++)
                dst[i] = csv;
            sw.Stop();
            Console.WriteLine($"New (threaded)! Allocated {rowsToParse} records in {sw.Elapsed}!");

            sw.Restart();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            var header = parserThreadedNoAlloc.Initialise(fs);
            
            parserThreadedNoAlloc.Parse(ref header, fs, dst, 0);
            sw.Stop();
            Console.WriteLine($"New (threaded)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }
        //ms.Position = 0;

        {
            Stopwatch sw = new();
            sw.Start();
            var csv = new GTFSStopTimeStructFast(1024);
            var dst = new GTFSStopTimeStructFast[rowsToParse];
            for (int i = 0; i < rowsToParse; i++)
                dst[i] = csv;
            sw.Stop();
            Console.WriteLine($"New (single)! Allocated {rowsToParse} records in {sw.Elapsed}!");

            sw.Restart();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            var header = parserNoAllocQuote.Initialise(fs);

            parserNoAllocQuote.Parse(ref header, fs, dst, 0);
            sw.Stop();
            Console.WriteLine($"New (single)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }
    }

    private static void TestLargeFile(string path, string pathq)
    {
        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ',',
            UseFastFloat = false,
        };
        var options2 = new CSVSerializerOptions(options)
        {
            HandleSpeechMarks = true
        };
        var options3 = new CSVSerializerOptions(options2)
        {
            Multithreaded = true,
            UseFastFloat = true
        };
        var parser = new CSVParser<GTFSStopTimeStruct>(options);
        var parserNoCusSer = new CSVParser<GTFSStopTimeStructNoCustomSer>(options);
        var parserNoAlloc = new CSVParser<GTFSStopTimeStructFast>(options);
        var parserNoAllocQuote = new CSVParser<GTFSStopTimeStructFast>(options2);
        var parserThreaded = new CSVParser<GTFSStopTimeStructNoCustomSer>(options3);
        var parserThreadedNoAlloc = new CSVParser<GTFSStopTimeStructFast>(options3);
        var parserOld = new CSVParserOld<GTFSStopTimeStruct>(options);

        int rowsToParse = 1_000_000;

#if false
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

        {
            Stopwatch sw = new();
            sw.Start();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            var header = parserNoAllocQuote.Initialise(fs);
            var csv = new GTFSStopTimeStructFast(1024);
            for (int i = 0; i < rowsToParse; i++)
                parserNoAllocQuote.ParseRow(ref header, fs, ref csv);
            sw.Stop();
            Console.WriteLine($"New (No alloc quote)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }

#else
        // Warmup
        {
            using FileStream fs = File.OpenRead(path);
            var csv = parser.Parse(fs).Take(rowsToParse/4).ToList();
            fs.Position = 0;
            var csv1 = parserOld.Parse(fs).Take(rowsToParse/4).ToList();
            fs.Position = 0;
            var csv2 = parserThreaded.Parse(fs).Take(rowsToParse/4).ToList();
            fs.Position = 0;
            var csv3 = parserThreadedNoAlloc.Parse(fs).Take(rowsToParse/4).ToList();
            Console.WriteLine($"Warmup! Loaded {csv.Count} and {csv1.Count} and {csv2.Count} and {csv3.Count} records!");

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
        }

        {
            Stopwatch sw = new();
            sw.Start();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            var header = parserNoAllocQuote.Initialise(fs);
            var csv = new GTFSStopTimeStructFast(1024);
            for (int i = 0; i < rowsToParse; i++)
                parserNoAllocQuote.ParseRow(ref header, fs, ref csv);
            sw.Stop();
            Console.WriteLine($"New (No alloc quote)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }

        Thread.Sleep(1500);

        {
            Stopwatch sw = new();
            sw.Start();
            var dst = new GTFSStopTimeStructNoCustomSer[rowsToParse];
            //for (int i = 0; i < rowsToParse; i++)
            //    dst[i] = new GTFSStopTimeStructNoCustomSer();
            sw.Stop();
            Console.WriteLine($"New (threaded)! Allocated {rowsToParse} records in {sw.Elapsed}!");

            sw.Restart();
            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            var header = parserThreaded.Initialise(fs);
            var csv = new GTFSStopTimeStructFast(1024);
            parserThreaded.Parse(ref header, fs, dst, 0);
            sw.Stop();
            Console.WriteLine($"New (threaded)! Loaded {rowsToParse} records in {sw.Elapsed}!");
        }

        Thread.Sleep(1500);

        {
            Stopwatch sw = new();
            sw.Start();

            //using var ms = new MemoryStream(File.ReadAllBytes(path));
            using FileStream fs = File.OpenRead(pathq);

            //var header = parserThreaded.Initialise(fs);
            var csv = new GTFSStopTimeStructFast(1024);
            float f = 0;
            foreach (var item in parserThreadedNoAlloc.Parse(fs).Take(rowsToParse))
                f += item.ShapeDistTraveled!.Value;
            sw.Stop();
            Console.WriteLine($"New (threaded, iterator)! Loaded {rowsToParse} records in {sw.Elapsed}!");
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

    private static void TestCSVParse<T>() where T : new()
    {
        var flds = typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public);
        var props = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var tmp = new T();

        foreach (var f in flds)
        {
            var ftype = f.FieldType;
            if (ftype == typeof(int))
                MakeSetter<T, int>(f)(ref tmp, 2);
            else if (ftype == typeof(bool))
                MakeSetter<T, bool>(f)(ref tmp, true);
            else if (ftype == typeof(ConsoleColor))
                MakeSetter<T, ConsoleColor>(f)(ref tmp, ConsoleColor.Magenta);
            else if (ftype == typeof(string))
                MakeSetter<T, string>(f)(ref tmp, "test");

            Console.WriteLine($"{f.Name} {f.GetValue(tmp)}");
        }
        foreach (var f in props)
        {
            var ftype = f.PropertyType;
            if (ftype == typeof(int))
            {
                var setter = MakeSetter<T, int>(f);
                setter(ref tmp, 2);
            }
            else if (ftype == typeof(bool))
                MakeSetter<T, bool>(f)(ref tmp, true);
            else if (ftype == typeof(ConsoleColor))
                MakeSetter<T, ConsoleColor>(f)(ref tmp, ConsoleColor.Magenta);
            else if (ftype == typeof(string))
                MakeSetter<T, string>(f)(ref tmp, "test");

            Console.WriteLine($"{f.Name} {f.GetValue(tmp)}");
        }
        Console.WriteLine();
    }

    private delegate void SetValue<T, U>(ref T target, U value);

    private static SetValue<T, U> MakeSetter<T, U>(FieldInfo fi)
    {
        if (false || RuntimeFeature.IsDynamicCodeSupported)
        {
            DynamicMethod setter = new($"set_{fi.Name}", null, [fi.DeclaringType!.MakeByRefType(), fi.FieldType], fi.DeclaringType!, true);
            var il = setter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            //il.Emit(OpCodes.Ldflda);
            il.Emit(OpCodes.Stfld, fi);
            il.Emit(OpCodes.Ret);
            return setter.CreateDelegate<SetValue<T, U>>();
        }
        else
        {
            return (ref T target, U value) =>
            {
                var tref = __makeref(target);
                fi.SetValueDirect(tref, value!);
            };
        }
    }

    private static SetValue<T, U> MakeSetter<T, U>(PropertyInfo pi)
    {
        if (true)
        {
            var setter = pi.GetSetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            DynamicMethod dynSetter = new($"set_{pi.Name}_{pi.GetHashCode()}", null, [typeof(T).MakeByRefType(), typeof(U)], typeof(T).Module);
            var il = dynSetter.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            if (!typeof(T).IsValueType)
                il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Ldarg_1);
            if (typeof(T).IsValueType)
                il.Emit(OpCodes.Call, setter);
            else
                il.Emit(OpCodes.Callvirt, setter);
            il.Emit(OpCodes.Ret);
            return dynSetter.CreateDelegate<SetValue<T, U>>();
        }
        else
        {
            /*var getter = pi.GetGetMethod() ?? throw new CSVSerializerException($"Property '{pi.Name}' of type {pi.PropertyType.Name} is not supported!");
            var get = getter.CreateDelegate<GetValueGeneric<U>>();
            return (ref T target) => get(target);*/
            return (ref T target, U val) =>
            {
                if (target != null)
                {
                    object box = target;
                    pi.SetValue(target, val);
                    target = (T)box; // Booo, unboxing...
                }
            };
        }
    }

    struct Test
    {
        public int a;
        public bool b;
        public ConsoleColor col;
        public string d;
    }

    class TestProps
    {
        public int a { get; set; }
        public bool b { get; set; }
        public ConsoleColor col { get; set; }
        public string d { get; set; }
    }

    private static readonly string csv = """
integer,str,enm,a,b,c
0,thomas,1,3,1,4
1,adam,1,5,9,2
2,test,6,5,3,5
""";

    public static void TestCSVParser()
    {
        var parser = new CSVParser<TestRow>(new CSVSerializerOptions() { IncludeFields = true });
        using MemoryStream ms = new(Encoding.UTF8.GetBytes(csv));
        var res = parser.Parse(ms).ToList();
        foreach (var row in res)
            Console.WriteLine(row.integer);
        Console.WriteLine("DONE");
    }

    public struct TestRow
    {
        public int integer;
        public string str;
        public TestEnum enm;
        [CSVIndex(4)]
        [CSVName("b")]
        public int bb;
        [CSVSkip]
        public int c;
    }

    public enum TestEnum
    {
        None = 0,
        One = 1,
        Two = 2,
    }
}
