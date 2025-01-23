// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using OpenSearch.Client;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.OpenSearch
{
    public class OpenSearchTestClient
    {
        protected const string IndexName = "flights";   // Must be lowercase!

        private OpenSearchClient _client;

        // Using the Elasticsearch server credentials since it OpenSearch supports it.

        protected Uri Address
        {
            get
            {
                return new Uri(ElasticSearchConfiguration.ElasticServer);
            }
        }
        protected string Username
        {
            get
            {
                return ElasticSearchConfiguration.ElasticUserName;
            }
        }
        protected string Password
        {
            get
            {
                return ElasticSearchConfiguration.ElasticPassword;
            }
        }

        public OpenSearchTestClient() { }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task ConnectAsync()
        {
            var settings = new ConnectionSettings(Address);
            settings.BasicAuthentication(Username, Password);
            settings.DefaultIndex(IndexName);
            _client = new OpenSearchClient(settings);

            // This isn't necessary but will log the response, which can help troubleshoot if
            // you're having connection errors
            _ = await _client.PingAsync();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Index()
        {
            var record = FlightRecord.GetSample();
            var response = _client.Index(record, i => i.Index(IndexName));

            if (!response.IsValid)
            {
                throw new Exception($"Response was not successful. {response.ServerError}");
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();
            var req = new IndexRequest<FlightRecord>();

            var response = await _client.IndexAsync(record, i => i.Index(IndexName));

            AssertResponseIsSuccess(response);

            return response.IsValid;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Search()
        {
            var response = _client.Search<FlightRecord>(s => s
                .Index(IndexName)
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.Field(t => t.Departure)
                    .Value(FlightRecord.GetSample().Departure)
                    )
                )
            );
            AssertResponseIsSuccess(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<FlightRecord>(s => s
                .Index(IndexName)
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.Field(t => t.Departure)
                    .Value(FlightRecord.GetSample().Departure)
                    )
                )
            );

            AssertResponseIsSuccess(response);

            return response.Total;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void IndexMany()
        {
            var records = FlightRecord.GetSamples(3);

            var response = _client.IndexMany(records, (IndexName)IndexName);

            AssertResponseIsSuccess(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<bool> IndexManyAsync()
        {
            var records = FlightRecord.GetSamples(3);

            var response = await _client.IndexManyAsync(records, IndexName);

            AssertResponseIsSuccess(response);

            return response.IsValid;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public long MultiSearch()
        {
            var response = _client.MultiSearch(IndexName, ms => ms
                .Index(IndexName).Search<FlightRecord>(s => s
                .Query(q => q
                    .Term(t => t.Field(t => t.Departure)
                    .Value(FlightRecord.GetSample().Departure)
                    )
                )));

            AssertResponseIsSuccess(response);
            return response.TotalResponses;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<long> MultiSearchAsync()
        {
            var response = await _client.MultiSearchAsync(IndexName, ms => ms
                .Index(IndexName).Search<FlightRecord>(s => s
                .Query(q => q
                    .Term(t => t.Field(t => t.Departure)
                    .Value(FlightRecord.GetSample().Departure)
                    )
                )));

            AssertResponseIsSuccess(response);
            return response.TotalResponses;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task GenerateErrorAsync()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ConnectionSettings(Address);
            settings.BasicAuthentication(Username, "12345");
            settings.DefaultIndex(IndexName);
            var client = new OpenSearchClient(settings);

            var response = await client.PingAsync();

            if (response.IsValid)
            {
                throw new Exception("Expected the call to fail, but it succeeded.");
            }
        }

        private static void AssertResponseIsSuccess<T>(T response)
            where T : class
        {
            if (response is ResponseBase responseBase)
            {
                if (!responseBase.IsValid)
                {
                    throw new Exception($"Response was not successful. {responseBase.ServerError}");
                }
            }
            else
            {
                throw new ArgumentException("Unsupported response type: " + response.GetType().FullName);
            }
        }
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
