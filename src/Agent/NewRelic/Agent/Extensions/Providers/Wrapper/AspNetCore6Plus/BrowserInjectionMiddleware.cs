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

        public Task Invoke(HttpContext context)
        {
            if (!BrowserInjectingStreamWrapper.Disabled)
            {
                // wrap the response body in our stream wrapper which will inject the RUM script if appropriate
                using var injectedResponse = new BrowserInjectingStreamWrapper(_agent, context.Response.Body, context);
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));
            }

            return _next(context);
        }
    }
}
