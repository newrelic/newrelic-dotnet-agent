// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using NewRelic.Agent.Api;

namespace NewRelic.Providers.Wrapper.AspNetCore6Plus
{

    /// <summary>
    /// Startup filter to add new relic elements into application pipeline.
    /// 
    /// This class is marked internal because it is not meant to be used by any agent dynamic type loading.
    /// Changing to public can result in handled type load errors on console applications due to the usage of IStartupFilter
    /// </summary>
    internal class AddNewRelicStartupFilter : IStartupFilter
    {
        private readonly IAgent _agent;

        public AddNewRelicStartupFilter(IAgent agent)
        {
            _agent = agent;
        }

        // TODO: browser injection middleware has to be injected before any response compression middleware, but after the AspNetCore wrapper middleware. Somehow.
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return builder =>
            {
                builder.UseMiddleware<WrapPipelineMiddleware>(_agent);
                builder.UseMiddleware<BrowserInjectionMiddleware>(_agent);
                next(builder);
            };
        }
    }
}
