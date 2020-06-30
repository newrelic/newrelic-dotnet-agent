/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AspNetCoreMvcFrameworkAsyncApplication
{
    public class UninstrumentedMiddleware
    {
        private readonly RequestDelegate _next;

        public UninstrumentedMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            await Task.Delay(1);
            await _next(context);
        }

    }
}
