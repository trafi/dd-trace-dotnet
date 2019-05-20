using System;
using System.Collections.Generic;
using AutoMapper;

namespace AutomapperTest
{
    public class Program
    {
        public static void Main()
        {
            Mapper.Initialize(
                configuration =>
                {
                    configuration.CreateMap<Model1, Model2>();
                });

            Console.WriteLine("Done");
        }
    }

    public class Model1
    {
        public List<string> Items { get; set; }
    }

    public class Model2
    {
        public List<string> Items { get; set; }
        //public string[] Items { get; set; }
    }
}
