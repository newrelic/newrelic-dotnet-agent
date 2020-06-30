/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using RestSharp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using static BasicMvcApplication.Controllers.RestAPIController;

namespace BasicMvcApplication.Controllers
{
    public class DistributedTracingController : Controller
    {
        [Route("DistributedTracing/Initiate")]
        public string Initiate()
        {
            NewRelic.Api.Agent.NewRelic.NoticeError(new DivideByZeroException());
            return "I noticed an error";
        }

        [Route("DistributedTracing/ReceivePayload")]
        public string ReceivePayload()
        {
            NewRelic.Api.Agent.NewRelic.NoticeError(new DivideByZeroException());
            return "I noticed an error after receiving a DT Payload";
        }

        [Route("DistributedTracing/SupportabilityReceivePayload")]
        public string SupportabilityReceivePayload()
        {
            return "I received a DT payload for supportability metrics.";
        }

        [Route("DistributedTracing/SupportabilityCreatePayload")]
        public async Task<string> SupportabilityCreatePayload()
        {
            var address = "http://www.google.com";

            using (var client = new HttpClient())
            {
                var result = await client.GetStringAsync(address);
            }

            return $"I created a distributed trace payload and sent it to {address}";
        }

        [Route("DistributedTracing/MakeExternalCallUsingRestClient")]
        public async Task<string> MakeExternalCallUsingRestClient(string externalCallUrl)
        {
            var uri = new Uri(externalCallUrl);
            var client = new RestClient($"http://{uri.Host}:{uri.Port}");
            var restRequest = new RestRequest(uri.PathAndQuery);
            var response = await client.ExecuteTaskAsync<IEnumerable<Bird>>(restRequest);

            if ((response.StatusCode != HttpStatusCode.OK) && (response.StatusCode != HttpStatusCode.NoContent))
            {
                return $"Unexpected HTTP status code {response.StatusCode}";
            }
            return "Worked";
        }
    }
}
