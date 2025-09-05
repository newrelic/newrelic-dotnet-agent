// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET9_0
using System;
using System.Collections.Generic;
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

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.RabbitMQ;

[Library]
public class RabbitMQModernExerciser
{
    private static readonly ConnectionFactory ChannelFactory = new ConnectionFactory()
    {
        HostName = RabbitMqConfiguration.RabbitMqServerIp,
        UserName = RabbitMqConfiguration.RabbitMqUsername,
        Password = RabbitMqConfiguration.RabbitMqPassword
    };

    private static IConnection Connection;
    private static IChannel Channel;

    // "User" headers to be set when publishing messages and then read when recieving them.
    // This verifies that our instrumentation does not overwrite or modify user headers.
    // A SortedDictionary is used to verify that the instrumentation interacts with the message
    // headers through the IDictionary interface.
    // See https://github.com/newrelic/newrelic-dotnet-agent/issues/639 for context
    private static IDictionary<string, object> userHeaders = new Dictionary<string, object>(new SortedDictionary<string, object>() { { "aNumber", 123 }, { "aString", "foo" } });


    [LibraryMethod]
    public async Task ConnectAsync()
    {
        Connection = await ChannelFactory.CreateConnectionAsync();
        Channel = await Connection.CreateChannelAsync();
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendReceiveAsync(string queueName, string message)
    {
        if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

        await DeclareQueueAsync(queueName);

        await BasicPublishMessageAsync(queueName, message);

        var receiveMessage = await BasicGetMessageAsync(queueName);

        await DeleteQueueAsync(queueName);

        // This sleep ensures that this transaction method is the one sampled for transaction trace data
        Thread.Sleep(1000);

        Console.WriteLine($"method=SendReceive,sent message={message},received message={receiveMessage}, queueName={queueName}");
    }


    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendReceiveWithEventingConsumerAsync(string queueName, string message)
    {
        if (string.IsNullOrEmpty(message)) { message = "Caller provided no message."; }

        await DeclareQueueAsync(queueName);

        await BasicPublishMessageAsync(queueName, message);

        var receiveMessage = await EventingConsumerGetMessageAsync(queueName);

        await DeleteQueueAsync(queueName);

        Console.WriteLine(
            $"method=SendReceiveWithEventingConsumer,sent message={message},received message={receiveMessage}, queueName={queueName}");
    }



    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendReceiveTopicAsync(string exchangeName, string topicName, string message)
    {
        //Publish
        await Channel.ExchangeDeclareAsync(exchange: exchangeName, type: "topic");

        var routingKey = topicName;
        var body = Encoding.UTF8.GetBytes(message);
        await Channel.BasicPublishAsync(exchangeName, routingKey, false, body);

        //Consume
        var queueName = (await Channel.QueueDeclareAsync()).QueueName;

        await Channel.QueueBindAsync(queue: queueName,
            exchange: exchangeName,
            routingKey: routingKey);

        var basicGetResult = await Channel.BasicGetAsync(queueName, true);

        //Cleanup
        await DeleteExchangeAsync(exchangeName);
        await DeleteQueueAsync(queueName);

        Console.WriteLine($"method=SendReceiveTopic,exchangeName={exchangeName},queueName={queueName},topicName={topicName},message={message},basicGetResult={basicGetResult}");
    }


    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendReceiveTempQueueAsync(string message)
    {
        var queueName = (await Channel.QueueDeclareAsync()).QueueName;

        await BasicPublishMessageAsync(queueName, message);

        var resultMessage = await BasicGetMessageAsync(queueName);

        Console.WriteLine($"method=SendReceiveTempQueue,queueName={queueName},sent message={message},received message={resultMessage}");
    }


    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task<string> QueuePurgeAsync(string queueName)
    {
        await DeclareQueueAsync(queueName);

        await BasicPublishMessageAsync(queueName, "I will be purged");

        var countMessages = await Channel.QueuePurgeAsync(queueName);

        return $"Purged {countMessages} message from queue: {queueName}";
    }

    [LibraryMethod]
    public async Task ShutdownAsync()
    {
        await Channel.CloseAsync();
        await Connection.CloseAsync();
        Console.WriteLine("RabbitMQ channel and connection closed.");
    }

    [LibraryMethod]
    public void PrintVersion()
    {
        var rabbitAssembly = Assembly.GetAssembly(typeof(ConnectionFactory));
        var assemblyPath = rabbitAssembly.Location;
        var rabbitClientVersion = FileVersionInfo.GetVersionInfo(assemblyPath).FileVersion;

        Console.WriteLine($"RabbitMQ client assembly path={assemblyPath}, version={rabbitClientVersion}");
        Console.WriteLine($"RabbitMQ client assembly path={assemblyPath}, version={rabbitClientVersion}");
    }

    private async Task DeleteQueueAsync(string queueName)
    {
        await Channel.QueueDeleteAsync(queueName, false, false);
    }

    private async Task DeleteExchangeAsync(string exchangeName)
    {
        await Channel.ExchangeDeleteAsync(exchangeName, false);
    }

    private async Task DeclareQueueAsync(string queueName)
    {
        await Channel.QueueDeclareAsync(queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);
    }

    private async Task BasicPublishMessageAsync(string queueName, string message)
    {

        var body = Encoding.UTF8.GetBytes(message);
        var props = new BasicProperties
        {
            Headers = userHeaders
        };

        await Channel.BasicPublishAsync("", queueName, false, props, body);
    }

    private async Task<string> BasicGetMessageAsync(string queueName)
    {
        var basicGetResult = await Channel.BasicGetAsync(queueName, true);

        VerifyHeaders(basicGetResult.BasicProperties.Headers);

        var receiveMessage = Encoding.UTF8.GetString(basicGetResult.Body.ToArray());

        return receiveMessage;
    }

    private async Task<string> EventingConsumerGetMessageAsync(string queueName)
    {
        using (var manualResetEvent = new ManualResetEventSlim(false))
        {
            var receivedMessage = string.Empty;
            var consumer = new AsyncEventingBasicConsumer(Channel);
            consumer.ReceivedAsync += handlerAsync;
            await Channel.BasicConsumeAsync(queueName, true, consumer);
            manualResetEvent.Wait();
            return receivedMessage;

            Task handlerAsync(object ch, BasicDeliverEventArgs basicDeliverEventArgs)
            {
                receivedMessage = Encoding.UTF8.GetString(basicDeliverEventArgs.Body.ToArray());
                VerifyHeaders(basicDeliverEventArgs.BasicProperties.Headers);

                InstrumentedChildMethod(); // to verify we're getting a child span

                manualResetEvent.Set();

                return Task.CompletedTask;
            }
        }
    }

    [Trace]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    private void InstrumentedChildMethod()
    {
        Thread.Sleep(100);
    }

    private void VerifyHeaders(IDictionary<string, object> headers)
    {
        foreach (var userHeader in userHeaders)
        {
            object objectFromMessageHeaders;
            if (headers.TryGetValue(userHeader.Key, out objectFromMessageHeaders))
            {
                var headerValuesMatch = true;
                if (objectFromMessageHeaders.GetType() == typeof(byte[]))
                {
                    //RabbitMQ encodes strings as byte arrays when sending messages
                    var decodedString = Encoding.UTF8.GetString((byte[])objectFromMessageHeaders);
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
#endif
