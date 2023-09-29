// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Hosting;

namespace NewRelic.Providers.Wrapper.AspNetCore.BrowserInjection
{
    internal class BrowserInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHostApplicationLifetime _lifetime;

        public BrowserInjectionMiddleware(RequestDelegate next, IHostApplicationLifetime lifetime)
        {
            _next = next;
            _lifetime = lifetime;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await using var injectedResponse = new ResponseStreamWrapper(context.Response.Body, context);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));

            await _next(context);
        }
    }
}
