// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MassTransit;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MassTransitLegacy
{
    public class MassTransitHelpers
    {
        private static string[] GetQueueData(Uri sourceAddress)
        {
            // rabbitmq://localhost/NRHXPSQL3_MassTransitTest_bus_iyeoyyge44oc7yijbdp5i1opfd?temporary=true
            var items = sourceAddress.AbsoluteUri.Split('_');
            return items[items.Length - 1].Split('?');
        }

        public static string GetQueueName(Uri sourceAddress)
        {
            if (sourceAddress == null)
            {
                return "Unknown";
            }
            var queueData = GetQueueData(sourceAddress);
            return queueData[0];
        }

        public static MessageBrokerDestinationType GetBrokerDestinationType(Uri sourceAddress)
        {
            if (sourceAddress != null)
            {
                var queueData = GetQueueData(sourceAddress);
                if (queueData.Length == 2 && queueData[1] == "temporary=true")
                {
                    return MessageBrokerDestinationType.TempQueue;
                }
            }

            return MessageBrokerDestinationType.Queue;
        }

        public static void InsertDistributedTraceHeaders(SendHeaders headers, ITransaction transaction)
        {
            var setHeaders = new Action<SendHeaders, string, string>((carrier, key, value) =>
            {
                carrier.Set(key, value);
            });

            transaction.InsertDistributedTraceHeaders(headers, setHeaders);
        }
    }
}
