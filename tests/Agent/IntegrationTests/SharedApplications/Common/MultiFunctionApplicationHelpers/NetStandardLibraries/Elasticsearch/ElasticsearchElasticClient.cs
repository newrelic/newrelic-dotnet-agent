// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using NewRelic.Agent.IntegrationTests.Shared;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    internal class ElasticsearchElasticClient : ElasticsearchTestClient
    {
        private ElasticsearchClient _client;

        public override void Connect()
        {
            var settings = new ElasticsearchClientSettings(Address)
                    .Authentication(new BasicAuthentication(ElasticSearchConfiguration.ElasticUserName,
                    ElasticSearchConfiguration.ElasticPassword)).
                    DefaultIndex(IndexName);

            _client = new ElasticsearchClient(settings);

            // TODO: This isn't necessary but will log the response, which can help troubleshoot if
            // you're having connection errors
            _client.Ping();
        }

        public override void Index()
        {
            var record = FlightRecord.GetSample();
            var response = _client.Index(record, IndexName);

            // TODO: Validate that it worked
        }

        public override async Task<bool> IndexAsync()
        {
            var record = FlightRecord.GetSample();

            var response = await _client.IndexAsync(record, IndexName);

            // TODO: Validate that it worked

            return response.IsSuccess();
        }

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

            // TODO: Validate that it worked
        }

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

            // TODO: Validate that it worked

            return response.Total;
        }

    }
}
