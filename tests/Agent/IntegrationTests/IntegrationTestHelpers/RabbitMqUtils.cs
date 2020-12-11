// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class RabbitMqUtils
    {
        public static void DeleteQueuesAndExchanges(IList<string> queues, IList<string> exchanges)
        {
            var factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
            using (var connection = factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    foreach (var queue in queues)
                    {
                        channel.QueueDeleteNoWait(queue, false, false);
                    }

                    foreach (var exchange in exchanges)
                    {
                        channel.ExchangeDeleteNoWait(exchange, false);
                    }
                }
            }
        }
    }
}
