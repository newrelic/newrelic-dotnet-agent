// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Net;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class HttpWebRequestLibrary
    {
        [Transaction]
        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void GetAll(string uri)
        {
            var tap = DoTAP(uri);
            var beginEnd = DoBeginEnd(uri);

            DoSync(uri);

            Task.WaitAll(tap, beginEnd);
        }

        [Transaction]
        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void GetSync(string uri)
        {
            DoSync(uri);
        }

        [Transaction]
        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void GetAsync_TAP(string uri)
        {
            DoTAP(uri).Wait();
        }

        [Transaction]
        [LibraryMethod]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void GetAsync_BeginEnd(string uri)
        {
            DoBeginEnd(uri).Wait();
        }

        [Trace]
        void DoSync(string uri)
        {
            var request = HttpWebRequest.CreateHttp(uri);

            try
            {
                using (var response = request.GetResponse() as HttpWebResponse)
                {

                }
            }
            catch (WebException) { }
        }

        [Trace]
        async Task DoTAP(string uri)
        {
            var request = HttpWebRequest.CreateHttp(uri);

            try
            {
                using (var response = await request.GetResponseAsync() as HttpWebResponse)
                {

                }
            }
            catch (WebException) { }
        }

        [Trace]
        async Task DoBeginEnd(string uri)
        {
            var request = HttpWebRequest.CreateHttp(uri);

            try
            {
                using (var response = await Task.Factory.FromAsync(request.BeginGetResponse, request.EndGetResponse, null) as HttpWebResponse)
                {

                }
            }
            catch (WebException) { }
        }
    }
}
