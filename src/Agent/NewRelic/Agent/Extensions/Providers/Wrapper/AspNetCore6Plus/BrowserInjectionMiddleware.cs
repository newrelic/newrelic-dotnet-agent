// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    internal class BrowserInjectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IAgent _agent;
        private readonly bool _runMiddleware;

        public BrowserInjectionMiddleware(RequestDelegate next, IAgent agent, string runBefore)
        {
            // Check if the next middleware is of the required type
            var fullName = next?.Target?.GetType().FullName;
            _runMiddleware = fullName == runBefore;
            agent.Logger.Log(Level.Finest, $"BrowserInjectionMiddleware: Next middleware is: {fullName}. Middleware {(_runMiddleware ? "will" : "will not")} run.");

            _next = next;
            _agent = agent;
        }

        public Task Invoke(HttpContext context)
        {
            if (_runMiddleware)
            {
                using var injectedResponse = new ResponseStreamWrapper(_agent, context.Response.Body, context);
                context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));
            }

            return _next(context);
        }
    }
}
