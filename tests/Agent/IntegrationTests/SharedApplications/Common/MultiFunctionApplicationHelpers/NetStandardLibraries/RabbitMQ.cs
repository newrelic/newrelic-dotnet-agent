// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class RabbitMQ
    {
        private static readonly ConnectionFactory ChannelFactory = new ConnectionFactory() { HostName = RabbitMqConfiguration.RabbitMqServerIp };
        private static readonly IConnection Connection = ChannelFactory.CreateConnection();
        private static IModel Channel = Connection.CreateModel();

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendReceive(string queueName, string message)
        {
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            DeclareQueue(queueName);

            BasicPublishMessage(queueName, message);

            var receiveMessage = BasicGetMessage(queueName);

            DeleteQueue(queueName);

            Logger.Info(string.Format("method=SendReceive,sent message={0},received message={1}, queueName={2}", message, receiveMessage, queueName));
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendReceiveWithEventingConsumer(string queueName, string message)
        {
            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            DeclareQueue(queueName);

            BasicPublishMessage(queueName, message);

            var receiveMessage = EventingConsumerGetMessage(queueName);

            DeleteQueue(queueName);

            Logger.Info(string.Format("method=SendReceiveWithEventingConsumer,sent message={0},received message={1}, queueName={2}", message, receiveMessage, queueName));
        }


        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendReceiveTopic(string exchangeName, string topicName, string message)
        {
            //Publish
            Channel.ExchangeDeclare(exchange: exchangeName, type: "topic");

            var routingKey = topicName;
            var body = Encoding.UTF8.GetBytes(message);
            Channel.BasicPublish(exchange: exchangeName,
                                    routingKey: routingKey,
                                    basicProperties: null,
                                    body: body);

            //Consume
            var queueName = Channel.QueueDeclare().QueueName;

            Channel.QueueBind(queue: queueName,
                                exchange: exchangeName,
                                routingKey: routingKey);

            var basicGetResult = Channel.BasicGet(queueName, true);

            //Cleanup
            DeleteExchange(exchangeName);
            DeleteQueue(queueName);

            Logger.Info($"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message},basicGetResult={basicGetResult}");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendReceiveTempQueue(string message)
        {
            var queueName = Channel.QueueDeclare().QueueName;

            BasicPublishMessage(queueName, message);

            var resultMessage = BasicGetMessage(queueName);

            Logger.Info($"method=SendReceiveTempQueue,queueName={queueName},sent message={message},received message={resultMessage}");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public string QueuePurge(string queueName)
        {
            DeclareQueue(queueName);

            BasicPublishMessage(queueName, "I will be purged");

            var countMessages = Channel.QueuePurge(queueName);

            return $"Purged {countMessages} message from queue: {queueName}";
        }

        //[LibraryMethod]
        public void DeleteQueue(string queueName)
        {
            Channel.QueueDeleteNoWait(queueName, false, false);
        }

        //[LibraryMethod]
        public void DeleteExchange(string exchangeName)
        {
            Channel.ExchangeDeleteNoWait(exchangeName, false);
        }

        [LibraryMethod]
        public void Shutdown()
        {
            Channel.Close();
            Connection.Close();
            Logger.Info("RabbitMQ channel and connection closed.");
        }

        [LibraryMethod]
        public void PrintVersion()
        {
            var rabbitAssembly = Assembly.GetAssembly(typeof(ConnectionFactory));
            var assemblyPath = rabbitAssembly.Location;
            var rabbitClientVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;

            Logger.Info($"RabbitMQ client assembly path={assemblyPath}, version={rabbitClientVersion}");
        }

        private void DeclareQueue(string queueName)
        {
            Channel.QueueDeclare(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        private void BasicPublishMessage (string queueName, string message)
        {

            var body = Encoding.UTF8.GetBytes(message);

            Channel.BasicPublish(exchange: "",
                routingKey: queueName,
                basicProperties: null,
                body: body);
        }

        private string BasicGetMessage(string queueName)
        {
            var basicGetResult = Channel.BasicGet(queueName, true);
#if NETCOREAPP3_1
            var receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body.ToArray());
#else
            var receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body);
#endif
            return receiveMessage;
        }

        private string EventingConsumerGetMessage(string queueName)
        {
            using (var manualResetEvent = new ManualResetEventSlim(false))
            {
                var receivedMessage = string.Empty;
                var consumer = new EventingBasicConsumer(Channel);
                consumer.Received += handler;
                Channel.BasicConsume(queueName, true, consumer);
                manualResetEvent.Wait();
                return receivedMessage;

                void handler(object ch, BasicDeliverEventArgs basicDeliverEventArgs)
                {
#if NETCOREAPP3_1
                    receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body.ToArray());
#else
                    receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body);
#endif
                    manualResetEvent.Set();
                }
            }
        }
    }
}
