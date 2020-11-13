// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreDistTracingApplication.Controllers
{
    public class FirstCallController : Controller
    {
        public async Task<string> CallNext(string nextUrl)
        {
            var client = new HttpClient();

            try
            {
                var result = await client.GetStringAsync(nextUrl);
                return result;
            }
            catch (Exception ex)
            {
                var result = $"Exception occurred in {nameof(FirstCallController)} calling [{nextUrl}]: {ex}";
                return result;
            }
        }

        public string WebRequestCallNext(string nextUrl)
        {
            var httpWebRequest = WebRequest.Create(nextUrl);
            httpWebRequest.GetResponse();
            return "Worked";
        }
    }
}
