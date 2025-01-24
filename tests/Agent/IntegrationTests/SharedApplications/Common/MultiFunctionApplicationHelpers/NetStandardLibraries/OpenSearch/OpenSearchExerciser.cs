// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.OpenSearch
{
    [Library]
    public class OpenSearchExerciser
    {
        private OpenSearchTestClient _client = new OpenSearchTestClient();

        [LibraryMethod]
        public async Task ConnectAsync() => await _client.ConnectAsync();

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
