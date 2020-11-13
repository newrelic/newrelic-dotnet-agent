// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
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
                string result = null;

                var client = new WebClient();
                result = await client.DownloadStringTaskAsync(new Uri(nextUrl));

                return result;
            }
            catch (Exception ex)
            {
                return $"Exception occurred in {nameof(SecondCallController)} calling [{nextUrl}]: {ex}";
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
