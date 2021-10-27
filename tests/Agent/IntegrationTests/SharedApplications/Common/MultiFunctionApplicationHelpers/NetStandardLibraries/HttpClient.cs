// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net.Http;
using System.Runtime.CompilerServices;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Internal
{
    [Library]
    internal static class HttpClient
    {
        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void Get() => Get("http://127.0.0.1");

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void Get(string uri)
        {
            try
            {
                using (var client = HttpClientFactory.Create())
                    client.GetAsync(uri).Wait();
            }
            catch { }
        }
    }
}
