// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class DefaultController : Controller
    {
        // GET: Default
        public ActionResult Index()
        {
            return View();
        }

        // GET: Query
        public ActionResult Query(string data)
        {
            return View("Index");
        }

        // GET: Ignored
        public string Ignored(string data)
        {
            NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
            return data;
        }

        // GET: CustomParameters
        public ActionResult CustomParameters(string key1, string value1, string key2, string value2)
        {
            NewRelic.Api.Agent.NewRelic.AddCustomParameter(key1, value1);
            NewRelic.Api.Agent.NewRelic.AddCustomParameter(key2, value2);

            Thread.Sleep(TimeSpan.FromSeconds(1));

            return View("Index");
        }

        [HttpGet]
        public void ThrowException()
        {
            throw new Exception("!Exception~Message!");
        }

        [HttpGet]
        public ActionResult SimulateLostTransaction()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            WebRequest.Create("https://www.newrelic.com").GetResponse();

            // Simulate lost transaction by clearing HttpContext
            HttpContext?.Items?.Clear();

            // Ensure that GC runs so that transaction can be recovered
            GC.Collect();
            GC.WaitForFullGCComplete();
            GC.WaitForPendingFinalizers();

            return View("Index");
        }

        [HttpGet]
        public async Task<string> HttpClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Do at least one request with a base address to ensure that we handle combining URLs correctly
            await new HttpClient { BaseAddress = new Uri("https://www.newrelic.com") }.GetStringAsync("/about");
            await new HttpClient().GetStringAsync("https://docs.newrelic.com");

            return "Worked";
        }

        [HttpGet]
        public async Task<string> HttpClientTaskCancelled()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = new TimeSpan(5);
                    await client.GetStringAsync("https://www.newrelic.org");
                }
            }
            catch (Exception)
            {
                //Swallow for test purposes
            }

            return "Worked";
        }

        [HttpGet]
        public string GetBrowserTimingHeader()
        {
            return NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader();
        }

        [HttpGet]
        public ActionResult GetHtmlWithCallToGetBrowserTimingHeader()
        {
            NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader();
            return View("Index");
        }

        public string NotHtmlContentType()
        {
            Response.ContentType = "application/json";
            return @"<html><head></head><body></body></html>";
        }

        public string DoRedirect(string data)
        {
            Response.Redirect("Index");
            return data;
        }

        public ActionResult StartAgent()
        {
            NewRelic.Api.Agent.NewRelic.StartAgent();
            return View("Index");
        }

        public string Chained(string chainedServerName, string chainedPortNumber, string chainedAction)
        {
            var address = $"http://{chainedServerName}:{chainedPortNumber}/Default/{chainedAction}";
            var httpWebRequest = WebRequest.Create(address);
            httpWebRequest.GetResponse();

            return "Worked";
        }

        public async Task<string> ChainedHttpClient(string chainedServerName, string chainedPortNumber, string chainedAction)
        {
            var address = $"http://{chainedServerName}:{chainedPortNumber}/Default/{chainedAction}";
            using (var client = new HttpClient())
            {
                await client.GetStringAsync(address);
            }

            return "Worked";
        }
    }
}
