/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using NServiceBus;
using System.Reflection;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace NServiceBusBasicMvcApplication
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public static IBus Bus { get; set; }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            Bus = CreateNServiceBus();
        }

        private static IBus CreateNServiceBus()
        {
            var configuration = new BusConfiguration();
            configuration.UsePersistence<InMemoryPersistence>();
            // limits the search for handlers to this specific assembly, otherwiese it will traverse all referenced assemblies looking for handlers.
            configuration.AssembliesToScan(Assembly.Load("NServiceBusReceiver"));
            return NServiceBus.Bus.Create(configuration);
        }
    }
}
