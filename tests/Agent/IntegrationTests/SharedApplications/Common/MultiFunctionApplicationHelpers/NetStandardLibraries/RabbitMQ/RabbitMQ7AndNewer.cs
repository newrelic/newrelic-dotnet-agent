// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET9_0 || NET481 // Other TFMs are tested in RabbitMQ6AndOlder
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    public class RabbitMQ7AndNewer
    {
        private IConnection _connection;
        private IChannel _channel;
        private bool _initialized;

        private static readonly ConnectionFactory ConnectionFactory = new()
        {
            HostName = RabbitMqConfiguration.RabbitMqServerIp,
            UserName = RabbitMqConfiguration.RabbitMqUsername,
            Password = RabbitMqConfiguration.RabbitMqPassword
        };

        // "User" headers to be set when publishing messages and then read when receiving them.
        // This verifies that our instrumentation does not overwrite or modify user headers.
        // A SortedDictionary is used to verify that the instrumentation interacts with the message
        // headers through the IDictionary interface.
        // See https://github.com/newrelic/newrelic-dotnet-agent/issues/639 for context
        private static readonly IDictionary<string, object> UserHeaders = new ReadOnlyDictionary<string, object>(new SortedDictionary<string, object>() { { "aNumber", 123 }, { "aString", "foo" } });

        [LibraryMethod]
        public async Task Initialize()
        {
            _connection = await ConnectionFactory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();
            ConsoleMFLogger.Info("RabbitMQ channel and connection created.");
            _initialized = true;
        }

        [LibraryMethod]
        public async Task Shutdown()
        {
            await _channel.CloseAsync();
            await _connection.CloseAsync();
            ConsoleMFLogger.Info("RabbitMQ channel and connection closed.");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SendReceive(string queueName, string message)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("RabbitMQ channel and connection not initialized.");
            }

            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            await DeclareQueue(queueName);

            await BasicPublishMessage(queueName, message);

            string receiveMessage = await BasicGetMessage(queueName);

            await DeleteQueue(queueName);

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);

            ConsoleMFLogger.Info(
                $"method=SendReceive,sent message={message},received message={receiveMessage}, queueName={queueName}");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SendReceiveWithEventingConsumer(string queueName, string message)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("RabbitMQ channel and connection not initialized.");
            }

            if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

            await DeclareQueue(queueName);

            await BasicPublishMessage(queueName, message);

            string receiveMessage = await EventingConsumerGetMessage(queueName);

            await DeleteQueue(queueName);

            ConsoleMFLogger.Info(
                $"method=SendReceiveWithEventingConsumer,sent message={message},received message={receiveMessage}, queueName={queueName}");
        }


        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SendReceiveTopic(string exchangeName, string topicName, string message)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("RabbitMQ channel and connection not initialized.");
            }

            //Publish
            await _channel.ExchangeDeclareAsync(exchange: exchangeName, type: "topic");

            string routingKey = topicName;
            byte[] body = Encoding.UTF8.GetBytes(message);
            await _channel.BasicPublishAsync(exchange: exchangeName,
                routingKey: routingKey,
                mandatory: true,
                body: body);

            //Consume
            string queueName = (await _channel.QueueDeclareAsync()).QueueName;

            await _channel.QueueBindAsync(queue: queueName,
                exchange: exchangeName,
                routingKey: routingKey);

            BasicGetResult basicGetResult = await _channel.BasicGetAsync(queueName, true);

            //Cleanup
            await DeleteExchange(exchangeName);
            await DeleteQueue(queueName);

            ConsoleMFLogger.Info($"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message},basicGetResult={basicGetResult}");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SendReceiveTempQueue(string message)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("RabbitMQ channel and connection not initialized.");
            }

            string queueName = (await _channel.QueueDeclareAsync()).QueueName;

            await BasicPublishMessage(queueName, message);

            string resultMessage = await BasicGetMessage(queueName);

            ConsoleMFLogger.Info($"method=SendReceiveTempQueue,queueName={queueName},sent message={message},received message={resultMessage}");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task<string> QueuePurge(string queueName)
        {
            if (!_initialized)
            {
                throw new InvalidOperationException("RabbitMQ channel and connection not initialized.");
            }

            await DeclareQueue(queueName);

            await BasicPublishMessage(queueName, "I will be purged");

            uint countMessages = await _channel.QueuePurgeAsync(queueName);

            return $"Purged {countMessages} message from queue: {queueName}";
        }

        public async Task DeleteQueue(string queueName)
        {
            await _channel.QueueDeleteAsync(queueName, false, false);
        }

        public async Task DeleteExchange(string exchangeName)
        {
            await _channel.ExchangeDeleteAsync(exchangeName, false);
        }

        [LibraryMethod]
        public void PrintVersion()
        {
            Assembly rabbitAssembly = Assembly.GetAssembly(typeof(ConnectionFactory));
            string assemblyPath = rabbitAssembly.Location;
            string rabbitClientVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;

            ConsoleMFLogger.Info($"RabbitMQ client assembly path={assemblyPath}, version={rabbitClientVersion}");
        }

        private async Task DeclareQueue(string queueName)
        {
            await _channel.QueueDeclareAsync(queue: queueName,
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: null);
        }

        private async Task BasicPublishMessage(string queueName, string message)
        {

            byte[] body = Encoding.UTF8.GetBytes(message);
            BasicProperties props = new BasicProperties { Headers = UserHeaders };

            await _channel.BasicPublishAsync(exchange: "",
                routingKey: queueName,
                mandatory: true,
                basicProperties: props,
                body: body);
        }

        private async Task<string> BasicGetMessage(string queueName)
        {
            BasicGetResult basicGetResult = await _channel.BasicGetAsync(queueName, true);

            if (basicGetResult != null)
            {
                VerifyHeaders(basicGetResult.BasicProperties.Headers);

                string receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body.ToArray());

                return receiveMessage;
            }

            return null;
        }

        private async Task<string> EventingConsumerGetMessage(string queueName)
        {
            using (ManualResetEventSlim manualResetEvent = new ManualResetEventSlim(false))
            {
                string receivedMessage = string.Empty;
                AsyncEventingBasicConsumer consumer = new AsyncEventingBasicConsumer(_channel);
                consumer.ReceivedAsync += handler;

                await _channel.BasicConsumeAsync(queueName, true, consumer);
                manualResetEvent.Wait();
                return receivedMessage;

                async Task handler(object ch, BasicDeliverEventArgs basicDeliverEventArgs)
                {
                    receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body.ToArray());

                    VerifyHeaders(basicDeliverEventArgs.BasicProperties.Headers);

                    await InstrumentedChildMethod(); // to verify we're getting a child span

                    manualResetEvent.Set();
                }
            }
        }

        [Trace]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private async Task InstrumentedChildMethod()
        {
            await Task.Delay(100);
        }

        private void VerifyHeaders(IDictionary<string, object> headers)
        {
            foreach (KeyValuePair<string, object> userHeader in UserHeaders)
            {
                object objectFromMessageHeaders;
                if (headers.TryGetValue(userHeader.Key, out objectFromMessageHeaders))
                {
                    bool headerValuesMatch = true;
                    if (objectFromMessageHeaders.GetType() == typeof(byte[]))
                    {
                        //RabbitMQ encodes strings as byte arrays when sending messages
                        string decodedString = Encoding.UTF8.GetString((byte[])objectFromMessageHeaders);
                        headerValuesMatch = (string)userHeader.Value == decodedString;
                    }
                    else
                    {
                        headerValuesMatch = userHeader.Value.Equals(objectFromMessageHeaders);
                    }
                    if (!headerValuesMatch)
                    {
                        throw new Exception("Header value in received message does not match expected value.");
                    }
                }
                else
                {
                    throw new Exception("Did not find expected user header value in received message.");
                }
            }
        }
    }
}
#endif
