// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class RabbitMqLegacyBasicMvcFixture : RemoteApplicationFixture
    {
        private readonly IList<string> _queues = new List<string>();
        private readonly IList<string> _exchanges = new List<string>();

        public RabbitMqLegacyBasicMvcFixture() : base(new RemoteWebApplication("RabbitMqLegacyBasicMvcApplication", ApplicationType.Unbounded))
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

        public override void Dispose()
        {
            base.Dispose();
            IntegrationTestHelpers.RabbitMqUtils.DeleteQueuesAndExchanges(_queues, _exchanges);
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

        public bool GetMessageQueue_RabbitMQ_SendReceive_HeaderExists(string message)
        {
            var queueName = GenerateQueue();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceive_HeaderExists?queueName={queueName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
                return Convert.ToBoolean(responseBody);
            }
        }

        public string GetMessageQueue_RabbitMQ_SendReceive_HeaderValue(string message)
        {
            var queueName = GenerateQueue();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceive_HeaderValue?queueName={queueName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
                return responseBody;
            }
        }

        public void GetMessageQueue_RabbitMQ_SendReceiveWithEventingConsumer_HeaderValue(string message)
        {
            var queueName = GenerateQueue();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceiveWithEventingConsumer?queueName={queueName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
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
