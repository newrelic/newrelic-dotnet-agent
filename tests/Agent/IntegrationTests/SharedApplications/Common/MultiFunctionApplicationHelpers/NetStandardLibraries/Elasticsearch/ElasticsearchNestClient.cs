// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Nest;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchNestClient : ElasticsearchTestClient
    {
        private ElasticClient _client;
        protected override Uri Address
        {
            get
            {
                return new Uri(ElasticSearch7Configuration.ElasticServer);
            }
        }
        protected override string Username
        {
            get
            {
                return ElasticSearch7Configuration.ElasticUserName;
            }
        }
        protected override string Password
        {
            get
            {
                return ElasticSearch7Configuration.ElasticPassword;
            }
        }

         [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Connect()
        {
            var settings = new ConnectionSettings(Address).
                BasicAuthentication(Username,Password).
                DefaultIndex(IndexName);

            _client = new ElasticClient(settings);

            // This isn't necessary but will log the response, which can help troubleshoot if
            // you're having connection errors
            _client.Ping();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
            var record = FlightRecord.GetSample();
            var response = _client.IndexDocument(record);

            AssertResponseIsValid(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();
            var response = await _client.IndexDocumentAsync(record);

            AssertResponseIsValid(response);

            return response.IsValid;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void IndexMany()
        {
            var records = FlightRecord.GetSamples(3);
            var response = _client.IndexMany(records);

            AssertResponseIsValid(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexManyAsync()
        {
            var record = FlightRecord.GetSample();
            var response = await _client.IndexDocumentAsync(record);

            AssertResponseIsValid(response);

            return response.IsValid;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Search()
        {
            var response = _client.Search<FlightRecord>(s => s
                .From(0)
                .Size(10)
                .Query(q => q
                    .Match(m => m
                    .Field(f => f.Departure)
                    .Query(FlightRecord.GetSample().Departure)
                    )
                   )
                );

            AssertResponseIsValid(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<FlightRecord>(s => s
                .From(0)
                .Size(10)
                .Query(q => q
                    .Match(m => m
                    .Field(f => f.Departure)
                    .Query(FlightRecord.GetSample().Departure)
                    )
                   )
                );

            AssertResponseIsValid(response);

            return response.Total;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void MultiSearch()
        {
            var msd = new MultiSearchDescriptor();

            var records = FlightRecord.GetSamples(2);
            foreach (var record in records)
            {
                msd.Search<FlightRecord>(s => s
                    .From(0)
                    .Size(10)
                    .Query(q => q
                        .Match(m => m
                        .Field(f => f.Departure)
                        .Query(record.Departure)
                        )
                       ));
            }

            var response = _client.MultiSearch(msd);

            AssertResponseIsValid(response);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> MultiSearchAsync()
        {
            var msd = new MultiSearchDescriptor();

            var records = FlightRecord.GetSamples(2);
            foreach (var record in records)
            {
                msd.Search<FlightRecord>(s => s
                    .From(0)
                    .Size(10)
                    .Query(q => q
                        .Match(m => m
                        .Field(f => f.Departure)
                        .Query(record.Departure)
                        )
                       ));
            }

            var response = await _client.MultiSearchAsync(msd);

            AssertResponseIsValid(response);

            return response.TotalResponses;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void GenerateError()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ConnectionSettings(Address).
                BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                "1234").
                DefaultIndex(IndexName);

            var client = new ElasticClient(settings);

            var response = client.Ping();
            if (response.IsValid)
            {
                throw new Exception($"Response was successful but we expected an error. {response.ServerError}");
            }
        }

        private static void AssertResponseIsValid<T>(T response)
            where T : IResponse
        {
            if (!response.IsValid)
            {
                throw new Exception($"Response was not successful. Elasitcsearch server error: {response.ServerError}");
            }
        }
    }
}
