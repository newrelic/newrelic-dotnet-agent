/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class DisableAsyncSupportController : Controller
    {
        protected override bool DisableAsyncSupport => true;

        public ActionResult Index()
        {
            return View();
        }
    }
}
