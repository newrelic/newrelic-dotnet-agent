/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/

using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using System.Messaging;

namespace MSMQBasicMVCApplication
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            CreateQueue("nrTestQueue");
            CreateQueue("nrTestQueueTransactional", true);

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        private static void CreateQueue(string queueName, bool isTransactional = false)
        {
            //We create the queue name here because this operation is only allowed on the current host and not remote ones.
            var privateQueueName = "private$\\" + queueName;

            if (
                MessageQueue.GetPrivateQueuesByMachine(Environment.MachineName)
                    .SingleOrDefault(x => x.QueueName.EndsWith(queueName.ToLower())) == null)
            {
                var localQueue = MessageQueue.Create(".\\" + privateQueueName, isTransactional);
                localQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl, AccessControlEntryType.Allow);
            }
        }
    }
}
