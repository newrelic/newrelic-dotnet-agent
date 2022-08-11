// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Threading;
using System.Web.Http;

namespace BasicMvcApplication.Controllers
{
    public class RestAPIController : ApiController
    {
        public class Bird
        {
            public string CommonName { get; set; }
            public string BandingCode { get; set; }
        }

        // GET: api/RestAPI
        public IEnumerable<Bird> Get()
        {
            return new Bird[] { new Bird { CommonName = "American Kestrel", BandingCode = "AMKE" }, new Bird { CommonName = "Snowy Owl", BandingCode = "SNOW" } };
        }

        // GET: api/RestAPI/5
        public Bird Get(int id)
        {
            //System.IO.File.AppendAllText(@"C:\IntegrationTestWorkingDirectory\RestAPIController.log", $"GET api/RestAPI/{id} called" + System.Environment.NewLine);
            if (id == 4)
            {
                // There is a test where a rest client is supposed to timeout when id of 4 is passed. This was sometimes completing before the client timed out.
                Thread.Sleep(10000);
            }
            return new Bird { CommonName = "Northern Flicker", BandingCode = "NOFL" };
        }

        // POST: api/RestAPI
        public void Post([FromBody] Bird bird)
        {
            //System.IO.File.AppendAllText(@"C:\IntegrationTestWorkingDirectory\RestAPIController.log", $"POST api/RestAPI called with {bird.CommonName} ({bird.BandingCode})" + System.Environment.NewLine);
        }

        // PUT: api/RestAPI/5
        public void Put(int id, [FromBody] Bird bird)
        {
            //System.IO.File.AppendAllText(@"C:\IntegrationTestWorkingDirectory\RestAPIController.log", $"PUT api/RestAPI/{id} called with {bird.CommonName} ({bird.BandingCode})" + System.Environment.NewLine);
        }

        // DELETE: api/RestAPI/5
        public void Delete(int id)
        {
            //System.IO.File.AppendAllText(@"C:\IntegrationTestWorkingDirectory\RestAPIController.log", $"DELETE api/RestAPI/{id} called" + System.Environment.NewLine);
        }
    }
}
