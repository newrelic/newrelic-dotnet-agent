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
        private readonly IList<String> _queues = new List<String>();
        private readonly IList<String> _exchanges = new List<String>();

        public RabbitMqBasicMvcFixture() : base(new RemoteWebApplication("RabbitMqBasicMvcApplication", ApplicationType.Unbounded))
        {
        }

        private String GenerateQueue()
        {
            var name = $"integrationTestQueue-{Guid.NewGuid()}";
            _queues.Add(name);
            return name;
        }

        private String GenerateExchange()
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

        public String GetMessageQueue_RabbitMQ_SendReceive(String message)
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

        public void GetMessageQueue_RabbitMQ_SendReceiveTopic(String topicName, String message)
        {
            var exchangeName = GenerateExchange();
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceiveTopic?exchangeName={exchangeName}&topicName={topicName}&message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public void GetMessageQueue_RabbitMQ_SendReceiveTempQueue(String message)
        {
            var address = $"http://{DestinationServerName}:{Port}/RabbitMQ/RabbitMQ_SendReceiveTempQueue?message={message}";

            using (var webClient = new WebClient())
            {
                var responseBody = webClient.DownloadString(address);
                Assert.NotNull(responseBody);
            }
        }

        public String GetMessageQueue_RabbitMQ_Purge()
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
