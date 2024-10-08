﻿using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSVParse.Benchmarks;

public record GTFSStopTime
{
    [CSVName("trip_id")]
#if CSVHelper
    [CsvHelper.Configuration.Attributes.Name("trip_id")]
#endif
    public string? TripID { get; init; }
    [CSVCustomSerializer<TransitTimeCSVSerializer>]
    [CSVName("arrival_time")]
#if CSVHelper
    [CsvHelper.Configuration.Attributes.Name("arrival_time")]
    [CsvHelper.Configuration.Attributes.TypeConverter(typeof(TransitTimeCSVConverter))]
#endif
    public TransitTime ArrivalTime { get; init; }
    [CSVCustomSerializer<TransitTimeCSVSerializer>]
    [CSVName("departure_time")]
#if CSVHelper
    [CsvHelper.Configuration.Attributes.Name("departure_time")]
    [CsvHelper.Configuration.Attributes.TypeConverter(typeof(TransitTimeCSVConverter))]
#endif
    public TransitTime DepartureTime { get; init; }
    [CSVName("stop_id")]
#if CSVHelper
    [CsvHelper.Configuration.Attributes.Name("stop_id")]
#endif
    public string? StopID { get; init; }
    //public int stop_sequence { get; init; }
    //public string stop_headsign { get; init; }
    //public GTFSPickupDropOffPattern? pickup_type { get; init; }
    //public GTFSPickupDropOffPattern? drop_off_type { get; init; }
    [CSVName("shape_dist_traveled")]
#if CSVHelper
    [CsvHelper.Configuration.Attributes.Name("shape_dist_traveled")]
#endif
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

public readonly struct GTFSStopTimeStructNoCustomSer
{
    [CSVName("trip_id")]
    [CSVIndex(0)]
    public readonly string TripID;
    [CSVName("arrival_time")]
    [CSVIndex(1)]
    public readonly TransitTime ArrivalTime;
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

public readonly struct GTFSStopTimeStructFast
{
    public GTFSStopTimeStructFast() : this(256)
    {

    }

    public GTFSStopTimeStructFast(int preallocateStringSize)
    {
        TripID = new PreAllocatedString(preallocateStringSize);
        StopID = new PreAllocatedString(preallocateStringSize);
    }

    [CSVName("trip_id")]
    [CSVIndex(0)]
    public readonly PreAllocatedString TripID;
    [CSVName("arrival_time")]
    [CSVIndex(1)]
    public readonly TransitTime ArrivalTime;
    [CSVName("departure_time")]
    [CSVIndex(2)]
    public readonly TransitTime DepartureTime;
    [CSVName("stop_id")]
    [CSVIndex(3)]
    public readonly PreAllocatedString StopID;
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

#if CSVHelper
public class TransitTimeCSVConverter : ITypeConverter
{
    public object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        return new TransitTime(text);
    }

    public string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
    {
        if (value is TransitTime time)
            return time.ToString();
        return null;
    }
}
#endif

public readonly struct TransitTime : ICSVSerializable
{
    public readonly int time;

    public TransitTime(ReadOnlySpan<char> s)
    {
        int hour = int.Parse(s[..2]);
        int min = int.Parse(s[3..5]);
        int second = int.Parse(s[6..8]);
        time = hour * 3600 + min * 60 + second;
    }

    public TransitTime(int seconds)
    {
        this.time = seconds;
    }

    public override string ToString()
    {
        var h = (time / 3600); // = 25
        var m = (time / 60 - (h * 60)); // = 30
        var s = time % 60;
        return $"{h:D2}:{m:D2}:{s:D2}";
    }

    public int Serialize(Span<char> dst)
    {
        throw new NotImplementedException();
    }
}
