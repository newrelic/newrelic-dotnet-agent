// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Web.Mvc;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using RestSharp;

namespace BasicMvcApplication.Controllers
{
    public class RestSharpController : Controller
    {
        private class Bird
        {
            public string CommonName { get; set; }
            public string BandingCode { get; set; }
        }
        [HttpGet]
        public string SyncClient(string method, bool generic)
        {
            var myHost = Request.Url.Host;
            var myPort = Request.Url.Port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = new Dictionary<string, IRestRequest>();
            requests.Add("GET", new RestRequest(endpoint + id));
            requests.Add("PUT", new RestRequest(endpoint + id, Method.PUT).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));
            requests.Add("DELETE", new RestRequest(endpoint + id, Method.DELETE));
            requests.Add("POST", new RestRequest(endpoint, Method.POST).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));

            if (generic)
            {
                var response = client.Execute<Bird>(requests[method]);
                if (method == "GET")
                {
                    var bird = response.Data;
                    System.IO.File.AppendAllText(@"C:\IntegrationTestWorkingDirectory\RestAPIController.log", $"SyncClient method={method}, generic={generic} got Bird {bird.CommonName} ({bird.BandingCode})" + Environment.NewLine);
                }
                if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    return $"Unexpected HTTP status code {response.StatusCode}";
                }
            }
            else
            {
                var response = client.Execute(requests[method]);
                if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
                {
                    return $"Unexpected HTTP status code {response.StatusCode}";
                }
            }
            return "Worked";
        }

        public async Task<string> AsyncAwaitClient(string method, bool generic, bool cancelable)
        {
            var myHost = Request.Url.Host;
            var myPort = Request.Url.Port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = new Dictionary<string, IRestRequest>();
            requests.Add("GET", new RestRequest(endpoint + id));
            requests.Add("PUT", new RestRequest(endpoint + id, Method.PUT).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));
            requests.Add("DELETE", new RestRequest(endpoint + id, Method.DELETE));
            requests.Add("POST", new RestRequest(endpoint, Method.POST).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));

            IRestResponse response;
            if (generic)
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    response = await client.ExecuteTaskAsync<Bird>(requests[method], cancellationTokenSource.Token);
                }
                else
                {
                    response = await client.ExecuteTaskAsync<Bird>(requests[method]);
                }
            }
            else
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    response = await client.ExecuteTaskAsync(requests[method], cancellationTokenSource.Token);
                }
                else
                {
                    response = await client.ExecuteTaskAsync(requests[method]);
                }
            }
            if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                return $"Unexpected HTTP status code {response.StatusCode}";
            }
            return "Worked";

        }

        public string TaskResultClient(string method, bool generic, bool cancelable)
        {
            var myHost = Request.Url.Host;
            var myPort = Request.Url.Port;
            var client = new RestClient($"http://{myHost}:{myPort}");

            var endpoint = "api/RestAPI/";
            var id = 1;

            var requests = new Dictionary<string, IRestRequest>();
            requests.Add("GET", new RestRequest(endpoint + id));
            requests.Add("PUT", new RestRequest(endpoint + id, Method.PUT).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));
            requests.Add("DELETE", new RestRequest(endpoint + id, Method.DELETE));
            requests.Add("POST", new RestRequest(endpoint, Method.POST).AddJsonBody(new { CommonName = "Painted Bunting", BandingCode = "PABU" }));

            IRestResponse response;
            if (generic)
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var task = client.ExecuteTaskAsync<Bird>(requests[method], cancellationTokenSource.Token);
                    response = task.Result;
                }
                else
                {
                    var task = client.ExecuteTaskAsync<Bird>(requests[method]);
                    response = task.Result;
                }
            }
            else
            {
                if (cancelable)
                {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var task = client.ExecuteTaskAsync(requests[method], cancellationTokenSource.Token);
                    response = task.Result;
                }
                else
                {
                    var task = client.ExecuteTaskAsync(requests[method]);
                    response = task.Result;
                }
            }
            if ((response.StatusCode != System.Net.HttpStatusCode.OK) && (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                return $"Non-200 HTTP status code {response.StatusCode}";
            }
            return "Worked";
        }

        [HttpGet]
        public async Task<string> RestSharpClientTaskCancelled()
        {
            var myHost = Request.Url.Host;
            var myPort = Request.Url.Port;

            var endpoint = "api/RestAPI/";
            var id = 4;

            try
            {
                var client = new RestClient($"http://{myHost}:{myPort}");
                client.Timeout = 1;
                await client.ExecuteTaskAsync(new RestRequest(endpoint + id));
            }
            catch (Exception)
            {
                //Swallow for test purposes
            }
            return "Worked";
        }
    }
}
