// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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

        public BrowserInjectionMiddleware(RequestDelegate next, IAgent agent)
        {
            _next = next;
            _agent = agent;
        }

        public Task Invoke(HttpContext context)
        {
            // short-circuit if we're not supposed to inject the browser script at all
            if (_agent.ShouldInjectBrowserScript(context.Response.ContentType, context.Request.Path.Value))
            {
                var originalResponseBodyFeature = context.Features.Get<IHttpResponseBodyFeature>();
                try
                {
                    // wrap the response body in our stream wrapper which will inject the RUM script if appropriate
                    using var injectedResponse = new BrowserInjectingStreamWrapper(_agent, context.Response.Body, context);
                    context.Features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(injectedResponse));
                }
                catch (Exception e) // don't let any exceptions in our code escape to user code
                {
                    // put the original response body feature back, log a message
                    _agent.Logger.Log(Level.Error, $"Exception in BrowserInjectionMiddleware: {e}");

                    if (originalResponseBodyFeature != null)
                        context.Features.Set<IHttpResponseBodyFeature>(originalResponseBodyFeature);
                }
            }

            return _next(context);
        }
    }
}
