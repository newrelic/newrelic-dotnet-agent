/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System.Web.Mvc;
using System.Web.Routing;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace RabbitMqLegacyBasicMvcApplication
{
    public class MvcApplication : System.Web.HttpApplication
    {
        private static readonly ConnectionFactory ChannelFactory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
        public static IModel Channel;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            InitializeRabbitMqChannel();
        }

        private void InitializeRabbitMqChannel()
        {
            var connection = ChannelFactory.CreateConnection();
            Channel = connection.CreateModel();
        }
    }
}
