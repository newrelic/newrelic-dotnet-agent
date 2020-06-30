/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Web.Http;
using Microsoft.Owin;
using Owin;

namespace Owin4WebApi
{
    public class Startup
    {
        // This code configures Web API. The Startup class is specified as a type parameter in the WebApp.Start method.
        public void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();

            appBuilder.MapWhen(ShouldUseBadMiddleware, app =>
            {
                app.Use<BadMiddleware>();
            });

            appBuilder.MapWhen(ShouldUseCustomMiddleware, app =>
            {
                app.Use<UninstrumentedMiddleware>();
                app.Use<CustomMiddleware>();
                app.UseWebApi(config);
            });
            appBuilder.UseWebApi(config);
        }

        private bool ShouldUseCustomMiddleware(IOwinContext context)
        {
            var shouldUse = context.Request.Path.Value.Contains("CustomMiddleware");
            return shouldUse;
        }

        private bool ShouldUseBadMiddleware(IOwinContext context)
        {
            var shouldUse = context.Request.Path.Value.Contains("BadMiddleware");
            return shouldUse;
        }
    }
}
