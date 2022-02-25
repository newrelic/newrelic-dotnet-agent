// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.W3C
{
    public class W3CController : ApiController
    {
        /// <summary>
        /// This is a test endpoint to allow the service to take in test json that calls back into itself.
        /// </summary>
        [Route("drop")]
        [HttpPost]
        public HttpResponseMessage DropThisData()
        {
            var value = Request.Content.ReadAsStringAsync().Result;
            if (string.IsNullOrEmpty(value))
            {
                ConsoleMFLogger.Error("POST body is null.");
                throw new NullReferenceException("POST body is null.");
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        /// <summary>
        /// This is the test endpoint.  The W3C test harness (python) will call this.
        /// </summary>
        [Route("test")]
        [HttpPost]
        public HttpResponseMessage Test()
        {
            var value = Request.Content.ReadAsStringAsync().Result;

            if (string.IsNullOrEmpty(value))
            {
                ConsoleMFLogger.Error("POST body is null.");
                throw new NullReferenceException("POST body is null.");
            }

            var models = JsonConvert.DeserializeObject<List<W3CTestModel>>(value);
            ProcessModels(models);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private void ProcessModels(List<W3CTestModel> models)
        {
            using (var client = new HttpClient())
            {
                foreach (var model in models)
                {
                    var request = BuildHttpRequest(model);
                    _ = client
                        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead)
                        .Result;
                }
            }
        }

        private HttpRequestMessage BuildHttpRequest(W3CTestModel model)
        {
            var argumentsJson = JsonConvert.SerializeObject(model.Arguments);
            return new HttpRequestMessage(HttpMethod.Post, model.Url)
            {
                Content = new StringContent(argumentsJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
