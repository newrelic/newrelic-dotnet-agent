// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Non-async client methods are deprecated in the latest Elastic.Clients.Elasticsearch
#if !NET481_OR_GREATER && !NET10_0_OR_GREATER
#define SYNC_METHODS_OK
#endif

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Core.MSearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Elastic.Transport;
using NewRelic.Agent.IntegrationTests.Shared;


namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchElasticClient : ElasticsearchTestClient
    {
        private ElasticsearchClient _client;

        private const string NonAsyncDeprecationMessage = "Non-async methods are deprecated in the latest Elasticsearch clients.";

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
        public override async Task ConnectAsync()
        {
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(Username, Password)).
                    DefaultIndex(IndexName);

            _client = new ElasticsearchClient(settings);

            // This isn't necessary but will log the response, which can help troubleshoot if
            // you're having connection errors
            _ = await _client.PingAsync();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
#if SYNC_METHODS_OK
            var record = FlightRecord.GetSample();
            var response = _client.Index(record, (IndexName)IndexName);

            if (!response.IsSuccess())
            {
                throw new Exception($"Response was not successful. {response.ElasticsearchServerError}");
            }
#else
            throw new NotImplementedException(NonAsyncDeprecationMessage);
#endif
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();
            var req = new IndexRequest<FlightRecord>();

            var response = await _client.IndexAsync(record, (IndexName)IndexName);

            AssertResponseIsSuccess(response);

            return response.IsSuccess();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Search()
        {
#if SYNC_METHODS_OK
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
#else
            throw new NotImplementedException(NonAsyncDeprecationMessage);
#endif
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> SearchAsync()
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
        public override void IndexMany()
        {
#if SYNC_METHODS_OK
            var records = FlightRecord.GetSamples(3);

            var response = _client.IndexMany(records, (IndexName)IndexName);

            AssertResponseIsSuccess(response);
#else
            throw new NotImplementedException(NonAsyncDeprecationMessage);
#endif
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexManyAsync()
        {
            var records = FlightRecord.GetSamples(3);

            var response = await _client.IndexManyAsync(records, IndexName);

            AssertResponseIsSuccess(response);

            return response.IsSuccess();
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void MultiSearch()
        {
#if SYNC_METHODS_OK
            var response = _client.MultiSearch<FlightRecord>();
#else
            throw new NotImplementedException(NonAsyncDeprecationMessage);
#endif
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> MultiSearchAsync()
        {
#if NET10_0_OR_GREATER || NET481_OR_GREATER
            var req = new MultiSearchRequest
            {
                Searches =
                [
                    new SearchRequestItem(
                        new MultisearchHeader { Indices = Infer.Index<FlightRecord>() },
                        new MultisearchBody { From = 0, Query = new MatchAllQuery() }
                    )
                ]
            };

            var response = await _client.MultiSearchAsync<FlightRecord>(req);
#else
            var response = await _client.MultiSearchAsync<FlightRecord>();
#endif

            return response.TotalResponses;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task GenerateErrorAsync()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    "12345")).
                    DefaultIndex(IndexName);

            var client = new ElasticsearchClient(settings);

            var response = await client.PingAsync();

            if (response.IsSuccess())
            {
                throw new Exception("Expected the call to fail, but it succeeded.");
            }
        }

        private static void AssertResponseIsSuccess<T>(T response)
            where T : Elastic.Transport.Products.Elasticsearch.ElasticsearchResponse
        {
            if (!response.IsSuccess())
            {
                throw new Exception($"Response was not successful. {response.ElasticsearchServerError}");
            }
        }
    }
}
