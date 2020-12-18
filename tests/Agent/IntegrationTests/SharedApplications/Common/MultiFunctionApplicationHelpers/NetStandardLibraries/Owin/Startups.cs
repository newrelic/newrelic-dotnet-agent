// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using Owin;
using System.Web.Http;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.Owin
{
    public interface IStartup
    {
        void Configuration(IAppBuilder appBuilder);
    }

    public class DefaultStartup : IStartup
    {
        public void Configuration(IAppBuilder appBuilder)
        {
            var config = new HttpConfiguration();
            config.MapHttpAttributeRoutes();
            appBuilder.UseWebApi(config);
        }
    }
}
