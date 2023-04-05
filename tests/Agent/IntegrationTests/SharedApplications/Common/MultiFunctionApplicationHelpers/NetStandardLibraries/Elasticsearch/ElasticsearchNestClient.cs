// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using System.Threading.Tasks;
using Nest;
using NewRelic.IntegrationTests.Models;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchNestClient : ElasticsearchTestClient
    {
        private ElasticClient _client;

        public override void Connect()
        {
            var settings = new ConnectionSettings(Address).DefaultIndex("people");

            _client = new ElasticClient(settings);
        }

        public override void Index()
        {
            var record = new FakeRecord("foo", "bar", "baz", 00000);
            var response = _client.IndexDocument(record);

        }

        public override async Task<bool> IndexAsync()
        {
            var record = new FakeRecord("foo", "bar", "baz", 00000);
            var response = await _client.IndexDocumentAsync(record);
            return response.IsValid;
        }

        public override void Search()
        {
            var searchResponse = _client.Search<FakeRecord>(s => s
                .From(0)
                .Size(10)
                .Query(q => q
                    .Match(m => m
                    .Field(f => f.LastName)
                    .Query("whatever")
                    )
                   )
                );
        }

        public override async Task<long> SearchAsync()
        {
            var response = await _client.SearchAsync<FakeRecord>(s => s
                .From(0)
                .Size(10)
                .Query(q => q
                    .Match(m => m
                    .Field(f => f.LastName)
                    .Query("something")
                    )
                   )
                );

            return response.Total;
        }
    }
}
