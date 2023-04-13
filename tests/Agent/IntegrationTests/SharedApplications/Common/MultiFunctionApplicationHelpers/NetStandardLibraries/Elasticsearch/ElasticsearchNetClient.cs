// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Elasticsearch.Net;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchNetClient : ElasticsearchTestClient
    {
        private ElasticLowLevelClient _client;

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Connect()
        {
            var settings = new ConnectionConfiguration(Address)
                .BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    ElasticSearchConfiguration.ElasticPassword)
                .RequestTimeout(TimeSpan.FromMinutes(2));

            _client = new ElasticLowLevelClient(settings);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Index()
        {
            var record = FlightRecord.GetSample();
            var indexResponse = _client.Index<BytesResponse>(IndexName, record.Id.ToString(), PostData.Serializable(record));
            byte[] responseBytes = indexResponse.Body;

            // TODO: Validate that it worked
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();

            var response = await _client.IndexAsync<StringResponse>(IndexName, record.Id.ToString(), PostData.Serializable(record));

            // TODO: Validate that it worked

            return response.Success;
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public override void Search()
        {
            var searchResponse = _client.Search<StringResponse>(IndexName, PostData.Serializable(new
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

            var successful = searchResponse.Success;
            var responseJson = searchResponse.Body;

            // TODO: Validate that it worked
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
            // TODO: Gotta parse the JSON :(
            var json = response.Body;
            IndexRequestParameters
            return 0;
        }

    }
}
