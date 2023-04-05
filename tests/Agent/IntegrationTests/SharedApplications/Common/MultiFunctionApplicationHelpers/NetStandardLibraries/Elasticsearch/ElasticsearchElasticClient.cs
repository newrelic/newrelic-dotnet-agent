// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using Azure;
using Elastic.Clients.Elasticsearch;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchElasticClient : ElasticsearchTestClient
    {
        private ElasticsearchClient _client;

        public override void Connect()
        {
            var settings = new ElasticsearchClientSettings(Address);

            _client = new ElasticsearchClient(settings);

            var record = new FakeRecord("foo", "bar", "baz", 00000);
        }

        public override void Index()
        {
            var record = new FakeRecord("foo", "bar", "baz", 00000);
            var response = _client.Index(record, "indexName");
        }

        public override async Task<bool> IndexAsync()
        {
            var record = new FakeRecord("foo", "bar", "home", 12345);

            var response = await _client.IndexAsync(record, "indexName");

            return response.IsSuccess();
        }

        public override void Search()
        {
            var response = _client.Search<FakeRecord>(s => s
                .Index("indexName")
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.LastName, "Bar")
                )
            );

            //response.Total
        }

        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<FakeRecord>(s => s
                .Index("indexName")
                .From(0)
                .Size(10)
                .Query(q => q
                    .Term(t => t.LastName, "Bar")
                )
            );

            return response.Total;
        }

    }
}
