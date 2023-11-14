// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;


namespace NewRelic.Agent.Extensions.Helpers
{
    public class MassTransitQueueData
    {
        public string QueueName { get; set; } = "Unknown";
        public MessageBrokerDestinationType DestinationType { get; set; } = MessageBrokerDestinationType.Queue;
    }

    public class MassTransitHelpers
    {
        public static MassTransitQueueData GetQueueData(Uri sourceAddress)
        {
            var data = new MassTransitQueueData();

            if (sourceAddress != null)
            {
                // rabbitmq://localhost/SomeHostname_MassTransitTest_bus_iyeoyyge44oc7yijbdp5i1opfd?temporary=true
                var items = sourceAddress.AbsoluteUri.Split('_');
                if (items.Length > 1)
                {
                    var queueData = items[items.Length - 1].Split('?');
                    data.QueueName = queueData[0];
                    if (queueData.Length == 2 && queueData[1] == "temporary=true")
                    {
                        data.DestinationType = MessageBrokerDestinationType.TempQueue;
                    }
                }
            }
            return data;
        }
    }
}
