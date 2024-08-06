using System.Reflection.Emit;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics;

namespace CSVParse.Test;

internal class Program
{
    static void Main(string[] args)
    {
        //Console.ReadLine();
        Console.WriteLine("Testing...");
        //TestCSVParse<Test>();
        //TestCSVParser();
        TestLargeFile();
        Console.WriteLine("DONE!");
        Console.ReadLine();
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

        int rowsToParse = 500000;

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

        {
            Stopwatch sw = new();
            sw.Start();
            using FileStream fs = File.OpenRead(path);
            var csv = parserOld.Parse(fs).Take(rowsToParse).ToList();
            sw.Stop();
            Console.WriteLine($"Old! Loaded {csv.Count} records in {sw.Elapsed}!");
        }
    }

    public record GTFSStopTime
    {
        [CSVName("trip_id")]
        public string? TripID { get; init; }
        [CSVCustomSerializer<TransitTimeCSVSerializer>]
        [CSVName("arrival_time")]
        public TransitTime ArrivalTime { get; init; }
        [CSVCustomSerializer<TransitTimeCSVSerializer>]
        [CSVName("departure_time")]
        public TransitTime DepartureTime { get; init; }
        [CSVName("stop_id")]
        public string? StopID { get; init; }
        //public int stop_sequence { get; init; }
        //public string stop_headsign { get; init; }
        //public GTFSPickupDropOffPattern? pickup_type { get; init; }
        //public GTFSPickupDropOffPattern? drop_off_type { get; init; }
        [CSVName("shape_dist_traveled")]
        public float? ShapeDistTraveled { get; init; }
        //public GTFSTimepoint timepoint { get; init; }
        //public GTFSPickupDropOffPattern? continuous_pickup { get; init; }
        //public GTFSPickupDropOffPattern? continuous_drop_off { get; init; }
    }

    public readonly struct GTFSStopTimeStruct
    {
        [CSVName("trip_id")]
        [CSVIndex(0)]
        public readonly string TripID;
        [CSVCustomSerializer<TransitTimeCSVSerializer>]
        [CSVName("arrival_time")]
        [CSVIndex(1)]
        public readonly TransitTime ArrivalTime;
        [CSVCustomSerializer<TransitTimeCSVSerializer>]
        [CSVName("departure_time")]
        [CSVIndex(2)]
        public readonly TransitTime DepartureTime;
        [CSVName("stop_id")]
        [CSVIndex(3)]
        public readonly string StopID;
        //public int stop_sequence { get; init; }
        //public string stop_headsign { get; init; }
        //public GTFSPickupDropOffPattern? pickup_type { get; init; }
        //public GTFSPickupDropOffPattern? drop_off_type { get; init; }
        [CSVName("shape_dist_traveled")]
        [CSVIndex(8)]
        public readonly float? ShapeDistTraveled;
        //public GTFSTimepoint timepoint { get; init; }
        //public GTFSPickupDropOffPattern? continuous_pickup { get; init; }
        //public GTFSPickupDropOffPattern? continuous_drop_off { get; init; }
    }

    public class TransitTimeCSVSerializer : ICustomCSVSerializer
    {
        public object? Deserialize(ReadOnlySpan<char> data, int lineNumber)
        {
            return new TransitTime(data);
        }
    }

    public readonly struct TransitTime
    {
        public readonly int time;

        public TransitTime(ReadOnlySpan<char> s)
        {
            int hour = int.Parse(s[..2]);
            int min = int.Parse(s[3..5]);
            int second = int.Parse(s[6..8]);
            time = hour * 3600 + min * 60 + second;
        }

        public override string ToString()
        {
            var h = (time / 3600); // = 25
            var m = (time / 60 - (h * 60)); // = 30
            var s = time % 60;
            return $"{h:D2}:{m:D2}:{s:D2}";
        }
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
        var res = parser.Parse(ms, CSVHeaderMode.Parse).ToList();
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
