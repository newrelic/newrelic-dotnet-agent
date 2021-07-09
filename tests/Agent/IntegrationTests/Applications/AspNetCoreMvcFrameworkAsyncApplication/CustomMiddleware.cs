// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Microsoft.AspNetCore.Http;
using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AspNetCoreMvcFrameworkAsyncApplication
{
    public class CustomMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        public async Task Invoke(HttpContext context)
        {
            await MiddlewareMethodAsync();
            await _next(context);
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private async Task MiddlewareMethodAsync()
        {
            await Task.Delay(1);
        }
    }
}
