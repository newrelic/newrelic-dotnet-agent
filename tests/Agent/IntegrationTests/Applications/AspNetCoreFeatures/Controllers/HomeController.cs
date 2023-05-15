// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using AspNetCoreFeatures.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Diagnostics;
using System.Threading;

namespace AspNetCoreFeatures.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            // Tests expect this to be the traced transaction, so let's help them out
            Thread.Sleep(1000);
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public void ThrowException()
        {
            throw new Exception("ExceptionMessage");
        }
    }
}
