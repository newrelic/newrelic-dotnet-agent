// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal abstract class ElasticsearchTestClient
    {
        protected const string IndexName = "flights";   // Must be lowercase!
        protected const string BadIndexName = "_ILLEGAL";

        protected abstract Uri Address
        {
            get;
        }
        protected abstract string Username
        {
            get;
        }
        protected abstract string Password
        {
            get;
        }

        public ElasticsearchTestClient() { }

        public abstract void Connect();

        public abstract void Index();

        public abstract Task<bool> IndexAsync();

        public abstract void IndexMany();

        public abstract Task<bool> IndexManyAsync();

        public abstract void Search();

        public abstract Task<long> SearchAsync();

        public abstract void MultiSearch();

        public abstract Task<long> MultiSearchAsync();

        public abstract void GenerateError();
    }

    public class FlightRecord
    {
        private static FlightRecord[] Samples =
        {
            new FlightRecord(1,  "PDX", "09:00", "DEN", "1:30"),
            new FlightRecord(2,  "ORD", "07:10", "LGA", "10:29"),
            new FlightRecord(3,  "ATL", "14:35", "IAD", "16:24"),
            new FlightRecord(4,  "YUL", "15:00", "EWR", "16:38"),
            new FlightRecord(5,  "BOS", "21:25", "ORD", "23:22"),
            new FlightRecord(6,  "IAD", "17:50", "GUA", "20:24"),
            new FlightRecord(7,  "ORD", "05:00", "DEN", "08:30"),
            new FlightRecord(8,  "IAD", "18:55", "ACC", "09:05"),
            new FlightRecord(9,  "MIA", "11:05", "EWR", "14:13"),
            new FlightRecord(10, "AMS", "12:00", "IAD", "14:15"),
        };
        public static FlightRecord GetSample(int which = 0) => Samples[which % Samples.Length];

        public static List<FlightRecord> GetSamples(int num) => Samples.Take(num % Samples.Length).ToList();

        protected FlightRecord(int id, string origin, string departure, string dest, string arrival) 
        {
            Id = id.ToString();
            Origin = origin;
            Departure = departure;
            Dest = dest;
            Arrival = arrival;
        }
        public string Id { get; set; }
        public string Origin { get; set; }
        public string Departure { get; set; }
        public string Dest { get; set; }
        public string Arrival { get; set; }
    }
}
