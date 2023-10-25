// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{
    internal class AddBrowserInjectionStartupFilter : IStartupFilter
    {
        private readonly IAgent _agent;
        private readonly string _runBefore;

        public AddBrowserInjectionStartupFilter(IAgent agent, string runBefore)
        {
            _agent = agent;
            _runBefore = runBefore;
        }

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                // wrap the builder with our interceptor
                var wrappedBuilder = new RunBeforeMiddlewareBuilder(builder, _agent, _runBefore);

                // build the rest of the pipeline using our wrapped builder
                next(wrappedBuilder);
            };
        }
    }
}
