// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Elasticsearch.Net;
using NewRelic.Agent.IntegrationTests.Shared;
using Xunit;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchNetClient : ElasticsearchTestClient
    {
        private ElasticLowLevelClient _client;
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
            var settings = new ConnectionConfiguration(Address)
                .BasicAuthentication(Username, Password)
                .RequestTimeout(TimeSpan.FromMinutes(2));

            _client = new ElasticLowLevelClient(settings);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
            var record = FlightRecord.GetSample();
            var response = _client.Index<BytesResponse>(IndexName, record.Id.ToString(), PostData.Serializable(record));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();

            var response = await _client.IndexAsync<StringResponse>(IndexName, record.Id.ToString(), PostData.Serializable(record));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");

            return response.Success;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void IndexMany()
        {
            var records = FlightRecord.GetSamples(3);
            var bulkIndex = new List<object>();

            foreach (var record in records)
            {
                bulkIndex.Add(new { index = new { _index = IndexName, _type = "FlightRecord",  _id = record.Id.ToString() } });
                bulkIndex.Add(record);
            }

            var response = _client.Bulk<BytesResponse>(PostData.MultiJson(bulkIndex));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexManyAsync()
        {
            var records = FlightRecord.GetSamples(3);
            var bulkIndex = new List<object>();

            foreach (var record in records)
            {
                bulkIndex.Add(new { index = new { _index = IndexName, _type = "FlightRecord", _id = record.Id.ToString() } });
                bulkIndex.Add(record);
            }

            var response = await _client.BulkAsync<BytesResponse>(PostData.MultiJson(bulkIndex));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");

            return response.Success;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Search()
        {
            var response = _client.Search<StringResponse>(IndexName, PostData.Serializable(new
            {
                from = 0,
                size = 10,
                query = new
                {
                    match = new
                    {
                        Departure = new
                        {
                            query = FlightRecord.GetSample().Departure
                        }
                    }
                }
            }));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<StringResponse>(IndexName, PostData.Serializable(new
            {
                from = 0,
                size = 10,
                query = new
                {
                    match = new
                    {
                        Departure = new
                        {
                            query = FlightRecord.GetSample().Departure
                        }
                    }
                }
            }));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");

            return 0;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void MultiSearch()
        {
            var records = FlightRecord.GetSamples(2);
            var multiSearchData = new List<object>();
            foreach (var record in records)
            {
                multiSearchData.Add(new { index = IndexName });
                multiSearchData.Add(new
                {
                    from = 0,
                    size = 10,
                    query = new
                    {
                        match = new
                        {
                            Departure = new
                            {
                                query = record.Departure
                            }
                        }
                    }
                });
            }
            var response = _client.MultiSearch<StringResponse>(IndexName, PostData.MultiJson(multiSearchData));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> MultiSearchAsync()
        {
            var records = FlightRecord.GetSamples(2);
            var multiSearchData = new List<object>();
            foreach (var record in records)
            {
                multiSearchData.Add(new { index = IndexName });
                multiSearchData.Add(new
                {
                    from = 0,
                    size = 10,
                    query = new
                    {
                        match = new
                        {
                            Departure = new
                            {
                                query = record.Departure
                            }
                        }
                    }
                });
            }

            var response = await _client.MultiSearchAsync<StringResponse>(IndexName, PostData.MultiJson(multiSearchData));

            Assert.True(response.Success, $"Elasticsearch server error: {response}");

            return 0;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void GenerateError()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ConnectionConfiguration(Address)
                .BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    "1234")
                .RequestTimeout(TimeSpan.FromMinutes(2));

            var client = new ElasticLowLevelClient(settings);
            var response = client.Ping<StringResponse>();

            Assert.False(response.Success, $"Elasticsearch server error: {response}");
        }
    }
}
