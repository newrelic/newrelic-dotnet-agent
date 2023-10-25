// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    /// <summary>
    /// Injects the BrowserInjectionMiddleware before *every* other middleware.
    /// Logic in BrowserInjectionMiddleware ensures that the middleware only runs before a specified middleware.
    /// </summary>
    internal class RunBeforeMiddlewareBuilder : IApplicationBuilder
    {
        private readonly IApplicationBuilder _inner;
        private readonly IAgent _agent;
        private readonly string _runBefore;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inner">The IApplicationBuilder to wrap</param>
        /// <param name="agent">The agent</param>
        /// <param name="runBefore">The full name of the middleware that the browser injection middleware should run just before</param>
        public RunBeforeMiddlewareBuilder(IApplicationBuilder inner, IAgent agent, string runBefore)
        {
            _inner = inner;
            _agent = agent;
            _runBefore = runBefore;
        }

        public IApplicationBuilder Use(Func<RequestDelegate, RequestDelegate> middleware)
        {
            return _inner
                .UseMiddleware<BrowserInjectionMiddleware>(_agent, _runBefore)
                .Use(middleware);
        }

        public IFeatureCollection ServerFeatures => _inner.ServerFeatures;
        public RequestDelegate Build() => _inner.Build();
        public IApplicationBuilder New() => _inner.New();
        public IServiceProvider ApplicationServices { get => _inner.ApplicationServices; set => _inner.ApplicationServices = value; }
        public IDictionary<string, object> Properties => _inner.Properties;
    }
}
