using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ
{
    [Library]
    public class RpcClient
    {
        private string replyQueueName;
        private EventingBasicConsumer consumer;
        private BlockingCollection<string> respQueue = new BlockingCollection<string>();
        private IBasicProperties props;

        private static readonly ConnectionFactory ChannelFactory = new ConnectionFactory()
        {
            HostName = RabbitMqConfiguration.RabbitMqServerIp,
            UserName = RabbitMqConfiguration.RabbitMqUsername,
            Password = RabbitMqConfiguration.RabbitMqPassword
        };
        private static readonly IConnection Connection = ChannelFactory.CreateConnection();
        private static IModel Channel = Connection.CreateModel();

        public void Setup()
        {
            replyQueueName = Channel.QueueDeclare().QueueName;
            consumer = new EventingBasicConsumer(Channel);

            props = Channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;

            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var response = Encoding.UTF8.GetString(body);
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    respQueue.Add(response);
                }
            };

            Channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);
        }

        public string Call(string message)
        {
            var messageBytes = Encoding.UTF8.GetBytes(message);
            Channel.BasicPublish(
                exchange: "",
                routingKey: "rpc_queue",
                basicProperties: props,
                body: messageBytes);

            return respQueue.Take();
        }

        public void Close()
        {
            Connection.Close();
        }
    }
}
