// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
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
#pragma warning disable CS0618 // obsolete usage is ok here
            _client.Ping();
#pragma warning restore CS0618
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
            var record = FlightRecord.GetSample();
#pragma warning disable CS0618 // Type or member is obsolete
            var response = _client.Index(record, (IndexName)IndexName);
#pragma warning restore CS0618 // Type or member is obsolete

            if (!response.IsSuccess())
            {
                throw new Exception($"Response was not successful. {response.ElasticsearchServerError}");
            }
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
#pragma warning disable CS0618 // obsolete usage is ok here
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
#pragma warning restore CS0618

            AssertResponseIsSuccess(response);
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
            var records = FlightRecord.GetSamples(3);

#pragma warning disable CS0618 // Type or member is obsolete
            var response = _client.IndexMany(records, (IndexName)IndexName);
#pragma warning restore CS0618 // Type or member is obsolete

            AssertResponseIsSuccess(response);
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
#if NET8_0_OR_GREATER || NET481_OR_GREATER
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
#pragma warning disable CS0618 // obsolete usage is ok here
            var response = _client.MultiSearch<FlightRecord>(req);
#pragma warning restore CS0618
#else
#pragma warning disable CS0618 // obsolete usage is ok here
            var response = _client.MultiSearch<FlightRecord>();
#pragma warning restore CS0618
#endif                        

        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<long> MultiSearchAsync()
        {
#if NET8_0_OR_GREATER || NET481_OR_GREATER
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
        public override void GenerateError()
        {
            // This isn't the password, so connection should fail, but we won't get an error until the Ping
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    "12345")).
                    DefaultIndex(IndexName);

            var client = new ElasticsearchClient(settings);

#pragma warning disable CS0618 // obsolete usage is ok here
            var response = client.Ping();
#pragma warning restore CS0618

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
