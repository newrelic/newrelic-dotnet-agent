// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace Owin4WebApi.Controllers;

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

    [HttpGet]
    [Route("api/CallNextWithExistingHeaders")]
    public async Task<string> CallNextWithExistingHeaders(string nextUrl)
    {
        try
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                // Pre-populate with stale DT headers — agent should replace them
                request.Headers.Add("traceparent", "00-stale0000000000000000000000000-stale000000000-01");
                request.Headers.Add("tracestate", "stale=value");
                request.Headers.Add("newrelic", "stale-newrelic-payload");
                var response = await client.SendAsync(request);
                var result = await response.Content.ReadAsStringAsync();
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