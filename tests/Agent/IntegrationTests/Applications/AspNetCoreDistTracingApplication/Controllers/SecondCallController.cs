// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreDistTracingApplication.Controllers
{
    public class SecondCallController : Controller
    {
        public async Task<string> CallNext(string nextUrl)
        {
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var result = await httpClient.GetStringAsync(nextUrl);
                    return result;
                }
            }
            catch (Exception ex)
            {
                return $"Exception occurred in {nameof(SecondCallController)} calling [{nextUrl}]: {ex}";
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
