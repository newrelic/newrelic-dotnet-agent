// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Owin3WebApi.Controllers
{
    public class ValuesController : ApiController
    {
        [HttpGet]
        [Route("api/ThrowException")]
        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }

        [HttpGet]
        [Route("api/Values")]
        public IEnumerable<string> Get()
        {
            return new string[]
            {
                "value 1",
                "value 2",
            };
        }

        [HttpGet]
        [Route("api/Values/{id}")]
        public string Get(uint id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }

        [HttpGet]
        [Route("api/Values")]
        public async Task<string> Get([FromUri] string data)
        {
            using (var client = new HttpClient())
            {
                await client.GetStringAsync("http://www.google.com");
            }

            // Attempt to enforce this is always the transaction trace.
            Thread.Sleep(5000);
            return data;
        }

        [HttpPost]
        [Route("api/Values")]
        public string Post([FromBody] string value)
        {
            return value;
        }

        [HttpPut]
        [Route("api/Values")]
        public string Put(uint id, [FromBody] string value)
        {
            return string.Format("{0}{1}", id, value);
        }

        [HttpDelete]
        [Route("api/Values")]
        public string Delete(uint id)
        {
            return id.ToString(CultureInfo.InvariantCulture);
        }

        [HttpGet]
        [Route("api/404")]
        public void Get404()
        {
            throw new HttpResponseException(HttpStatusCode.NotFound);
        }

        [HttpGet]
        [Route("api/Sleep")]
        public string Sleep()
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
            return "Great success";
        }

        [HttpGet]
        [Route("api/SegmentTerm")]
        public string SegmentTerm()
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
            return "Great success";
        }

        [HttpGet]
        [Route("api/UrlRule")]
        public string UrlRule()
        {
            Thread.Sleep(TimeSpan.FromSeconds(3));
            return "Great success";
        }

        [HttpGet]
        [Route("api/IgnoreTransaction")]
        public void IgnoreTransaction()
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
        }

    }
}
