/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using RabbitMQ.Client;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
    public class RabbitMqReceiverFixture : RemoteApplicationFixture
    {
        private readonly ConnectionFactory _factory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
        private const string Message = "Hello, Spaceman.";
        private const string ApplicationDirectoryName = "RabbitMqReceiverHost";
        private const string ExecutableName = "RabbitMqReceiverHost.exe";
        private const string TargetFramework = "net452";

        public string QueueName { get; }

        public RabbitMqReceiverFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Unbounded))
        {
            QueueName = $"integrationTestQueue-{Guid.NewGuid()}";
        }

        public void CreateQueueAndSendMessage()
        {
            using (var connection = _factory.CreateConnection())
            {
                using (var channel = connection.CreateModel())
                {
                    channel.QueueDeclare(queue: QueueName,
                        durable: false,
                        exclusive: false,
                        autoDelete: false,
                        arguments: null);

                    var body = Encoding.UTF8.GetBytes(Message);

                    channel.BasicPublish(exchange: "",
                        routingKey: QueueName,
                        basicProperties: null,
                        body: body);
                }
            }
        }

        private void DeleteQueue()
        {
            using (var connection = _factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeleteNoWait(QueueName, false, false);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            DeleteQueue();
        }
    }
}
