﻿using System.Text;

namespace CSVParse.Benchmarks
{
    internal static class TestDataGenerator
    {

        private const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWZYXabcdefghijklmnopqrstuvwxyz    12345678901234567890";//-=+}{#';/.,!£%^$&*)()";
        private static string GenerateRandomLine(int minLength, int maxLength)
        {
            //List<string> lines = [];
            StringBuilder sb = new();
            for (int j = 0; j < Random.Shared.Next(minLength, maxLength); j++)
            {
                sb.Append(alphabet[Random.Shared.Next(alphabet.Length)]);
            }
            return sb.ToString();
            //lines.Add(sb.ToString());
            //return [.. lines];
        }

        public static void GenerateTestData(string path, int rows)
        {
            var options = new CSVSerializerOptions()
            {
                IncludeFields = true,
                IncludeProperties = true,
                IncludePrivate = false,
                HandleSpeechMarks = false,
                Separator = ','
            };
            //var parser = new CSVParser<GTFSStopTimeStruct>(options);

            Console.WriteLine("Generating test data...");

            using var fs = File.CreateText(path);
            var rnd = Random.Shared;

            // Write header
            //public string? TripID { get; init; }
            fs.Write("trip_id");
            fs.Write(options.Separator);
            //public TransitTime ArrivalTime { get; init; }
            fs.Write("arrival_time");
            fs.Write(options.Separator);
            //public TransitTime DepartureTime { get; init; }
            fs.Write("departure_time");
            fs.Write(options.Separator);
            //public string? StopID { get; init; }
            fs.Write("stop_id");
            fs.Write(options.Separator);
            //public int stop_sequence { get; init; }
            fs.Write("stop_sequence");
            fs.Write(options.Separator);
            //public string stop_headsign { get; init; }
            fs.Write("stop_headsign");
            fs.Write(options.Separator);
            //public GTFSPickupDropOffPattern? pickup_type { get; init; }
            fs.Write("pickup_type");
            fs.Write(options.Separator);
            //public GTFSPickupDropOffPattern? drop_off_type { get; init; }
            fs.Write("drop_off_type");
            fs.Write(options.Separator);
            //public float? ShapeDistTraveled { get; init; }
            fs.Write("shape_dist_traveled");
            fs.Write(options.Separator);
            //public GTFSTimepoint timepoint { get; init; }
            fs.Write("timepoint");
            fs.Write(options.Separator);
            //public GTFSPickupDropOffPattern? continuous_pickup { get; init; }
            fs.Write("continuous_pickup");
            fs.Write(options.Separator);
            //public GTFSPickupDropOffPattern? continuous_drop_off { get; init; }
            fs.Write("continuous_drop_off");
            fs.WriteLine();

            // Generate data
            for (int i = 0; i < rows; i++)
            {
                //public string? TripID { get; init; }
                fs.Write(GenerateRandomLine(6, 12));
                fs.Write(options.Separator);
                //public TransitTime ArrivalTime { get; init; }
                fs.Write(new TransitTime(rnd.Next(120000)).ToString());
                fs.Write(options.Separator);
                //public TransitTime DepartureTime { get; init; }
                fs.Write(new TransitTime(rnd.Next(120000)).ToString());
                fs.Write(options.Separator);
                //public string? StopID { get; init; }
                fs.Write(GenerateRandomLine(6, 12));
                fs.Write(options.Separator);
                //public int stop_sequence { get; init; }
                fs.Write(rnd.Next(1000));
                fs.Write(options.Separator);
                //public string stop_headsign { get; init; }
                fs.Write(GenerateRandomLine(6, 12));
                fs.Write(options.Separator);
                //public GTFSPickupDropOffPattern? pickup_type { get; init; }
                fs.Write(rnd.Next(100));
                fs.Write(options.Separator);
                //public GTFSPickupDropOffPattern? drop_off_type { get; init; }
                fs.Write(rnd.Next(100));
                fs.Write(options.Separator);
                //public float? ShapeDistTraveled { get; init; }
                fs.Write(rnd.NextSingle() * 10000);
                fs.Write(options.Separator);
                //public GTFSTimepoint timepoint { get; init; }
                fs.Write(rnd.Next(100000));
                fs.Write(options.Separator);
                //public GTFSPickupDropOffPattern? continuous_pickup { get; init; }
                fs.Write(rnd.Next(3));
                fs.Write(options.Separator);
                //public GTFSPickupDropOffPattern? continuous_drop_off { get; init; }
                fs.Write(rnd.Next(3));
                //fs.Write(options.Separator);

                fs.WriteLine();

                if (i % (rows / 100) == 0)
                {
                    Console.WriteLine($"Generating data {i / (float)rows * 100:F2}%...");
                }
            }

            Console.WriteLine("Test file generated!");
        }
    }
}