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

        if (!File.Exists(path))
        {
            TestDataGenerator.GenerateTestData(path, 20_000_000, false);
            TestDataGenerator.GenerateTestData(pathq, 20_000_000, true);
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, true, true);
        }

        //TestCSVParse<Test>();
        //TestCSVParser();
        TestLargeFile(path, pathq);
        Console.WriteLine("DONE!");
        Console.ReadLine();
    }

    private static void TestLargeFile(string path, string pathq)
    {
        var options = new CSVSerializerOptions()
        {
            IncludeFields = true,
            IncludeProperties = true,
            IncludePrivate = false,
            HandleSpeechMarks = false,
            Separator = ','
        };
        var options2 = new CSVSerializerOptions(options)
        {
            HandleSpeechMarks = true
        };
        var parser = new CSVParser<GTFSStopTimeStruct>(options);
        var parserNoCusSer = new CSVParser<GTFSStopTimeStructNoCustomSer>(options);
        var parserNoAlloc = new CSVParser<GTFSStopTimeStructFast>(options);
        var parserNoAllocQuote = new CSVParser<GTFSStopTimeStructFast>(options2);
        var parserOld = new CSVParserOld<GTFSStopTimeStruct>(options);

        int rowsToParse = 1_000_0000;

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
            var csv = parser.Parse(fs).Take(rowsToParse).ToList();
            fs.Position = 0;
            var csv1 = parserOld.Parse(fs).Take(rowsToParse).ToList();
            Console.WriteLine($"Warmup! Loaded {csv.Count} and {csv1.Count} records!");

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
