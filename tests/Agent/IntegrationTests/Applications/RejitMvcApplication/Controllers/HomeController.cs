// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Web.Mvc;

namespace RejitMvcApplication.Controllers
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
