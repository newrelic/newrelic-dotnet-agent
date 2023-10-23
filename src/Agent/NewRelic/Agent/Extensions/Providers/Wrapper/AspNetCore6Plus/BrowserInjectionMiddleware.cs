// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    internal class BrowserInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAgent _agent;

        public BrowserInjectionMiddleware(RequestDelegate next, IAgent agent)
        {
            _next = next;
            _agent = agent;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await using var injectedResponse = new ResponseStreamWrapper(_agent, context.Response.Body, context);
            context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));

            await _next(context);
        }
    }
}
