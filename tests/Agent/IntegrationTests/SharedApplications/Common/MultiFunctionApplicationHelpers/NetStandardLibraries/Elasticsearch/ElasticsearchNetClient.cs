// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Elasticsearch.Net;
using NewRelic.IntegrationTests.Models;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchNetClient : ElasticsearchTestClient
    {
        private ElasticLowLevelClient _client;

        public override void Connect()
        {
            var settings = new ConnectionConfiguration(Address)
                .RequestTimeout(TimeSpan.FromMinutes(2));

            _client = new ElasticLowLevelClient(settings);
        }

        public override void Index()
        {
            var record = new FakeRecord("foo", "bar", "home", 12345);
            var indexResponse = _client.Index<BytesResponse>("people", "1", PostData.Serializable(record));
            byte[] responseBytes = indexResponse.Body;

        }

        public override async Task<bool> IndexAsync()
        {
            var record = new FakeRecord("foo", "bar", "home", 12345);

            var response = await _client.IndexAsync<StringResponse>("people", "1", PostData.Serializable(record));
            return response.Success;
        }

        public override void Search()
        {
            var searchResponse = _client.Search<StringResponse>("people", PostData.Serializable(new
            {
                from = 0,
                size = 10,
                query = new
                {
                    match = new
                    {
                        LastName = new
                        {
                            query = "Bar"
                        }
                    }
                }
            }));

            var successful = searchResponse.Success;
            var responseJson = searchResponse.Body;
        }

        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<StringResponse>("people", PostData.Serializable(new
            {
                from = 0,
                size = 10,
                query = new
                {
                    match = new
                    {
                        LastName = new
                        {
                            query = "Bar"
                        }
                    }
                }
            }));
            // Gotta parse the JSON :(
            var json = response.Body;
            return 0;
        }

    }
}
