// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SerilogSumologicApplication.Models;

namespace SerilogSumologicApplication.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index()
        {
            Log.Information("Index page loaded.");

            using (var client = new HttpClient())
            {
                var result = await client.GetStringAsync("http://www.google.com");
            }

            return View();
        }

        public IActionResult SyncControllerMethod()
        {
            ViewData["Message"] = "Your application description page.";
            Log.Information("About page loaded.");

            using (var client = new HttpClient())
            {
                var result = client.GetStringAsync("http://www.google.com");
                result.Wait();
            }

            return View();
        }

        public async Task<IActionResult> AsyncControllerMethod()
        {
            ViewData["Message"] = "Your application contact page.";
            Log.Information("Contact page loaded.");

            using (var client = new HttpClient())
            {
                var result = await client.GetStringAsync("http://www.google.com");
            }

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
