// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Threading;
using System.Web.Http;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.RestSharp
{
    public class RestAPIController : ApiController
    {
        public class Bird
        {
            public string CommonName { get; set; }
            public string BandingCode { get; set; }
        }

        [HttpGet]
        public IEnumerable<Bird> Get()
        {
            return new Bird[] { new Bird { CommonName = "American Kestrel", BandingCode = "AMKE" }, new Bird { CommonName = "Snowy Owl", BandingCode = "SNOW" } };
        }

        [HttpGet]
        public Bird Get(int id)
        {
            // If the ID is 4, this request is coming from the RestSharpClientTaskCancelled parent
            // endpoint and we need to ensure that the client times out before the request succeeds
            if (id == 4)
            {
                Thread.Sleep(100);
            }

            return new Bird { CommonName = "Northern Flicker", BandingCode = "NOFL" };
        }

        [HttpPost]
        public void Post([FromBody] Bird bird)
        {
            // Do nothing
        }

        [HttpPut]
        public void Put(int id, [FromBody] Bird bird)
        {
            // Do nothing
        }

        [HttpDelete]
        public void Delete(int id)
        {
            // Do nothing
        }
    }
}
