// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using AspNetCoreMvcRejitApplication.Models;

namespace AspNetCoreMvcRejitApplication.Controllers
{
    /// <summary>
    /// Simple endpoint to get the agent spun up.
    /// Specifically outside the CustomInstrumentationController to make it easier to see the real data.
    /// </summary>
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return Content("It am working", "text/plain");
        }
    }
}
