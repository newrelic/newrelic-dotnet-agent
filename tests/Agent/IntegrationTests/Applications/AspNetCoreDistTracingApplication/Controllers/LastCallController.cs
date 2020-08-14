// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using Microsoft.AspNetCore.Mvc;

namespace AspNetCoreDistTracingApplication.Controllers
{
    public class LastCallController : Controller
    {
        public string CallEnd()
        {
            return "Worked";
        }

        public string CallError()
        {
            throw new Exception("Borked");
        }
    }
}
