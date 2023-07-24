// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchElasticClient : ElasticsearchTestClient
    {
        private ElasticsearchClient _client;
        protected override Uri Address
        {
            get
            {
                return new Uri(ElasticSearchConfiguration.ElasticServer);
            }
        }
        protected override string Username
        {
            get
            {
                return ElasticSearchConfiguration.ElasticUserName;
            }
        }
        protected override string Password
        {
            get
            {
                return ElasticSearchConfiguration.ElasticPassword;
            }
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Connect()
        {
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(Username, Password)).
                    DefaultIndex(IndexName);

            _client = new ElasticsearchClient(settings);

            // This isn't necessary but will log the response, which can help troubleshoot if
            // you're having connection errors
            _client.Ping();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
            var record = FlightRecord.GetSample();
            var response = _client.Index(record, IndexName);

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();

            var response = await _client.IndexAsync(record, IndexName);

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");

            return response.IsSuccess();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Search()
        {
            var response = _client.Search<FlightRecord>(s => s
                .Index(IndexName)
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.Departure, FlightRecord.GetSample().Departure)
                )
            );

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<FlightRecord>(s => s
                .Index(IndexName)
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.Departure, FlightRecord.GetSample().Departure)
                )
            );

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");

            return response.Total;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void IndexMany()
        {
            var records = FlightRecord.GetSamples(3);

            var response = _client.IndexMany(records, IndexName);

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexManyAsync()
        {
            var records = FlightRecord.GetSamples(3);

            var response = await _client.IndexManyAsync(records, IndexName);

            Assert.True(response.IsSuccess(), $"Elasticsearch server error: {response.ElasticsearchServerError}");

            return response.IsSuccess();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void MultiSearch()
        {
            // Currently unable to figure out how to make a real multisearch work in 8.x
            // This empty call is enough to make the instrumentation (wrapper) execute and generate the data
            // we are looking for, but we can't assert for success.
            var response = _client.MultiSearch<FlightRecord>();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> MultiSearchAsync()
        {
            // Currently unable to figure out how to make a real multisearch work in 8.x
            // This empty call is enough to make the instrumentation (wrapper) execute and generate the data
            // we are looking for, but we can't assert for success.
            var response = await _client.MultiSearchAsync<FlightRecord>();

            return response.TotalResponses;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void GenerateError()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    "12345")).
                    DefaultIndex(IndexName);

            var client = new ElasticsearchClient(settings);

            var response = client.Ping();

            Assert.False(response.IsSuccess());
        }
    }
}
