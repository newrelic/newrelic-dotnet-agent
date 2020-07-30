/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class RabbitMqBasicMvcFixture : RemoteApplicationFixture
    {
        private readonly IList<string> _queues = new List<string>();
        private readonly IList<string> _exchanges = new List<string>();

        public RabbitMqBasicMvcFixture() : base(new RemoteWebApplication("RabbitMqBasicMvcApplication", ApplicationType.Unbounded))
        {
        }

        private string GenerateQueue()
        {
            var name = $"integrationTestQueue-{Guid.NewGuid()}";
            _queues.Add(name);
            return name;
        }

        private string GenerateExchange()
        {
            var name = $"integrationTestExchange-{Guid.NewGuid()}";
            _exchanges.Add(name);
            return name;
        }

        private void DeleteQueuesAndExchanges()
        {
            var factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                foreach (var queue in _queues)
                {
                    channel.QueueDeleteNoWait(queue, false, false);
                }

                foreach (var exchange in _exchanges)
                {
                    channel.ExchangeDeleteNoWait(exchange, false);
                }
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DeleteQueuesAndExchanges();
        }

        #region RabbitMQController Actions

        public string GetMessageQueue_RabbitMQ_SendReceive(string message)
        {
            var queueName = GenerateQueue();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceive?queueName={queueName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
                return queueName;
            }
        }

        public void GetMessageQueue_RabbitMQ_SendReceiveTopic(string topicName, string message)
        {
            var exchangeName = GenerateExchange();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceiveTopic?exchangeName={exchangeName}&topicName={topicName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMessageQueue_RabbitMQ_SendReceiveTempQueue(string message)
        {
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceiveTempQueue?message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public string GetMessageQueue_RabbitMQ_Purge()
        {
            var queueName = GenerateQueue();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_QueuePurge?queueName={queueName}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
                return queueName;
            }
        }

        #endregion
    }
}
