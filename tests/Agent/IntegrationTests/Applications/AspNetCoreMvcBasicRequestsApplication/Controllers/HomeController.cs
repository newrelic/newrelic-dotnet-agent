// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using AspNetCoreMvcBasicRequestsApplication.Models;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreMvcBasicRequestsApplication.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
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
    }
}
