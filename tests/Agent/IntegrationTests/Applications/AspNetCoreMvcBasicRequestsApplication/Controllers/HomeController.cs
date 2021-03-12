// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using AspNetCoreMvcBasicRequestsApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcBasicRequestsApplication.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly NewRelicDownloadSiteClient _nrDownloadSiteClient;

        public HomeController(IHttpClientFactory httpClientFactory, NewRelicDownloadSiteClient nrDownloadSiteClient)
        {
            _httpClientFactory = httpClientFactory;
            _nrDownloadSiteClient = nrDownloadSiteClient;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public IActionResult Query(string data)
        {
            return View();
        }

        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }

        [HttpGet]
        public async Task<string> HttpClient()
        {
            // Do at least one request with a base address to ensure that we handle combining URLs correctly
            await new HttpClient { BaseAddress = new Uri("http://www.google.com") }.GetStringAsync("/search");
            await new HttpClient().GetStringAsync("http://www.yahoo.com");

            return "Worked";
        }

        [HttpGet]
        public async Task<string> HttpClientTaskCancelled()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(5);
                    await client.GetStringAsync("http://www.bing.com");
                }
            }
            catch (Exception)
            {
                //Swallow for test purposes
            }

            return "Worked";
        }

        [HttpGet]
        public async Task<string> HttpClientFactory()
        {
            await _httpClientFactory.CreateClient().GetStringAsync("https://docs.newrelic.com/");

            return "Worked";
        }

        [HttpGet]
        public async Task<string> TypedHttpClient()
        {
            await _nrDownloadSiteClient.GetLatestReleaseAsync();

            return "Worked";
        }
    }
}
