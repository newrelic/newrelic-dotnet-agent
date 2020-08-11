// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace SerilogSumologicApplication
{

    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;


        public LoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task Invoke(HttpContext context)
        {
            string path = context.Request.Path;


            Console.WriteLine("####### Logging -- in theory.");
            Log.Information("Middleware logging. Request path is " + path);

            await _next(context);
            return;
        }
    }
}
