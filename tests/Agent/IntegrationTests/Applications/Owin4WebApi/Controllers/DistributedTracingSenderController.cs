// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Owin4WebApi.Controllers
{
    public class DistributedTracingSenderController : ApiController
    {
        [HttpGet]
        [Route("api/CallNext")]
        public async Task<string> CallNext(string nextUrl)
        {

            try
            {
                using (var client = new HttpClient())
                {
                    var result = await client.GetStringAsync(nextUrl);
                    return result;
                }
            }
            catch (Exception ex)
            {
                var result = $"Exception occurred in {nameof(DistributedTracingSenderController)} calling [{nextUrl}]: {ex}";
                return result;
            }
        }
    }
}
