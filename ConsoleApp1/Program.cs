namespace ConsoleApp1;

using System;
class Program
{
    static void Main()
    {
        //Console.WriteLine("");
        var t = new Props();
        Console.WriteLine(GetTest(ref t));
    }

    static int GetTest(ref Props obj)
    {
        return obj.Test;
    }

    static void SetTestRef(ref Props obj, int val)
    {
        obj.Test = val;
    }

    static void SetTest(Props obj, int val)
    {
        obj.Test = val;
    }

    static void SetTestRefC(ref PropsC obj, int val)
    {
        obj.Test = val;
    }

    static void SetTestC(PropsC obj, int val)
    {
        obj.Test = val;
    }
}

public struct Props
{
    public int Test { get; set; }
}

public class PropsC
{
    public int Test { get; set; }
}
