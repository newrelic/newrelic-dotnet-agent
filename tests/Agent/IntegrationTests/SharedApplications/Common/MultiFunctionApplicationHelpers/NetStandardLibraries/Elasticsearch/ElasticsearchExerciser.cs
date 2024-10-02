// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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

        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        /// <summary>
        /// Sets the library to use for further actions
        /// </summary>
        /// <param name="client">ElasticsearchNet, NEST, or ElasticClients</param>
        public async Task SetClient(string clientType)
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
                await _client.ConnectAsync();
            }
            else
            {
                // oops!
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Search() => _client.Search();

        [LibraryMethod]
        [Transaction]
        public async Task SearchAsync() => await _client.SearchAsync();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void MultiSearch() => _client.MultiSearch();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task MultiSearchAsync() => await _client.MultiSearchAsync();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Index() => _client.Index();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task IndexAsync() => await _client.IndexAsync();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void IndexMany() => _client.IndexMany();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task IndexManyAsync() => await _client.IndexManyAsync();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task GenerateErrorAsync() => await _client.GenerateErrorAsync();
    }
}
