// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Nest;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Elasticsearch
{
    [Library]
    public class ElasticsearchExerciser
    {
        private enum ClientType
        {
            ElasticsearchNet,
            NEST,
            ElasticClients
        }
        private ClientType _clientType;
        private ElasticsearchTestClient _client;
        private const string ASYNC_MODE = "async";

        [LibraryMethod]
        public void SetClient(string clientType)
        {
            if (Enum.TryParse(clientType, out ClientType client))
            {
                _clientType = client;
                switch (_clientType)
                {
                    case ClientType.NEST:
                        _client = new ElasticsearchNestClient();
                        break;
                    case ClientType.ElasticsearchNet:
                        _client = new ElasticsearchNetClient();
                        break;
                    case ClientType.ElasticClients:
                        _client = new ElasticsearchElasticClient();
                        break;
                }
            }
            else
            {
                // oops!
            }
        }

        [LibraryMethod]
        [Transaction]
        public void Search(string syncMode)
        {
            if (syncMode == ASYNC_MODE)
            {
                _client.SearchAsync();
            }
            else
            {
                _client.Search();
            }
        }

        [LibraryMethod]
        [Transaction]
        public void Index(string syncMode)
        {
            if (syncMode == ASYNC_MODE)
            {
                _client.IndexAsync();
            }
            else
            {
                _client.Index();
            }
        }

    }
}
