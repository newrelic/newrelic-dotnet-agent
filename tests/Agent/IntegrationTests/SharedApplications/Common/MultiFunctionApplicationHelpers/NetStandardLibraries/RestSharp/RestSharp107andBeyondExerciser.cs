// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RestSharp;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;

// .NET 4.8 and 4.8.1 test v107+ of RestSharp which has a different API than older versions
#if NET48_OR_GREATER

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp
{
    [Library]
    public class RestSharpExerciser
    {
        private class Bird
        {
            public string CommonName { get; set; }
            public string BandingCode { get; set; }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string SyncClient(string host, int port, string method, bool generic)
        {
            var myHost = host;
            var myPort = port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = GetRequests(endpoint, id);
            if (generic)
            {
                // This may cause hangs...
                var response = client.ExecuteAsync<Bird>(requests[method]).GetAwaiter().GetResult();
                if (method == "GET")
                {
                    var bird = response.Data;
                    ConsoleMFLogger.Info($"SyncClient method={method}, generic={generic} got Bird {bird.CommonName} ({bird.BandingCode})");
                }

                if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    return $"Unexpected HTTP status code {response.StatusCode}";
                }
            }
            else
            {
                var response = client.ExecuteAsync(requests[method]).GetAwaiter().GetResult();
                if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    return $"Unexpected HTTP status code {response.StatusCode}";
                }
            }

            return "Worked";
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> AsyncAwaitClient(string host, int port, string method, bool generic, bool cancelable)
        {
            var myHost = host;
            var myPort = port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = GetRequests(endpoint, id);
            RestResponse response;
            if (generic)
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    response = await ExecuteAsyncGeneric<Bird>(client, requests[method], cancellationTokenSource);
                }
                else
                {
                    response = await ExecuteAsyncGeneric<Bird>(client, requests[method]);
                }
            }
            else
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    response = await ExecuteAsync(client, requests[method], cancellationTokenSource);
                }
                else
                {
                    response = await ExecuteAsync(client, requests[method]);
                }
            }

            if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                return $"Unexpected HTTP status code {response.StatusCode}";
            }

            return "Worked";

        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string TaskResultClient(string host, int port, string method, bool generic, bool cancelable)
        {
            var myHost = host;
            var myPort = port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = GetRequests(endpoint, id);
            RestResponse response;
            if (generic)
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var task = ExecuteAsyncGeneric<Bird>(client, requests[method], cancellationTokenSource);
                    response = task.Result;
                }
                else
                {
                    var task = ExecuteAsyncGeneric<Bird>(client, requests[method]);
                    response = task.Result;
                }
            }
            else
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var task = ExecuteAsync(client, requests[method], cancellationTokenSource);
                    response = task.Result;
                }
                else
                {
                    var task = ExecuteAsync(client, requests[method]);
                    response = task.Result;
                }
            }

            if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                return $"Non-200 HTTP status code {response.StatusCode}";
            }

            return "Worked";
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> RestSharpClientTaskCancelled(string host, int port)
        {
            var myHost = host;
            var myPort = port;

            var endpoint = "api/RestAPI/";
            var id = 4;

            try
            {
                var options = new RestClientOptions($"http://{myHost}:{myPort}")
                {
#if NET481
                    MaxTimeout = 1
#else
                    Timeout = 1
#endif
                };
                var client = new RestClient(options);

                var response = await client.ExecuteAsync(new RestRequest(endpoint + "Get/" + id));
            }
            catch (Exception)
            {
                //Swallow for test purposes
            }

            return "Worked";
        }

#region Helpers

        private Dictionary<string, RestRequest> GetRequests(string endpoint, int id)
        {
            var requests = new Dictionary<string, RestRequest>();
            requests.Add("GET", new RestRequest(endpoint + "Get/" + id));
            requests.Add("PUT", new RestRequest(endpoint + "Put/" + id, Method.Put).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));
            requests.Add("DELETE", new RestRequest(endpoint + "Delete/" + id, Method.Delete));
            requests.Add("POST", new RestRequest(endpoint + "Post/", Method.Post).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));
            return requests;
        }

        private Task<RestResponse<T>> ExecuteAsyncGeneric<T>(RestClient client, RestRequest request, CancellationTokenSource cancellationTokenSource = null)
        {
            if (cancellationTokenSource == null)
            {
                return client.ExecuteAsync<T>(request);
            }

            return client.ExecuteAsync<T>(request, cancellationTokenSource.Token);
        }

        private Task<RestResponse> ExecuteAsync(RestClient client, RestRequest request, CancellationTokenSource cancellationTokenSource = null)
        {
            if (cancellationTokenSource == null)
            {
                return client.ExecuteAsync(request);
            }

            return client.ExecuteAsync(request, cancellationTokenSource.Token);
        }

#endregion

    }
}

#endif
