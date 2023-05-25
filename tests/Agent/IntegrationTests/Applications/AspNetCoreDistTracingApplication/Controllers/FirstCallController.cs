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
                var result = $"Exception occurred in {nameof(FirstCallController)} calling [{nextUrl}]: {ex}";
                return result;
            }
        }

        public string WebRequestCallNext(string nextUrl)
        {
#pragma warning disable SYSLIB0014 // obsolete usage is ok here
            var httpWebRequest = WebRequest.Create(nextUrl);
            httpWebRequest.GetResponse();
#pragma warning restore SYSLIB0014
            return "Worked";
        }
    }
}
