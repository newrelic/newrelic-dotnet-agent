// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus;

[Library]
internal class AzureServiceBusExerciser
{
    [LibraryMethod]
    public static async Task InitializeQueue(string queueName)
    {
        var adminClient = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);
        // if the queue exists, delete it and re-create it
        if (await adminClient.QueueExistsAsync(queueName))
        {
            await adminClient.DeleteQueueAsync(queueName);
        }
        await adminClient.CreateQueueAsync(queueName);

    }

    [LibraryMethod]
    public static async Task DeleteQueue(string queueName)
    {
        var adminClient = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);
        if (await adminClient.QueueExistsAsync(queueName))
        {
            await adminClient.DeleteQueueAsync(queueName);
        }
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task ExerciseMultipleReceiveOperationsOnAMessage(string queueName)
    {
        await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

        await SendAMessage(client, queueName, "Hello world!");

        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        await receiver.PeekMessageAsync();

        // receive the message in peek lock mode
        var receivedMessage = await receiver.ReceiveMessageAsync();

        // renew message lock
        await receiver.RenewMessageLockAsync(receivedMessage);

        // defer the message
        await receiver.DeferMessageAsync(receivedMessage);

        // receive the deferred message
        var deferredMessage = await receiver.ReceiveDeferredMessageAsync(receivedMessage.SequenceNumber);

        // complete the message
        await receiver.CompleteMessageAsync(deferredMessage);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task ScheduleAndReceiveAMessage(string queueName)
    {
        await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);
        await using var sender = client.CreateSender(queueName);

        var message = new ServiceBusMessage("Hello world!");
        await sender.ScheduleMessageAsync(message, DateTime.UtcNow.AddSeconds(5));

        await Task.Delay(TimeSpan.FromSeconds(10));

        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        await receiver.ReceiveMessageAsync();
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task ReceiveAndDeadLetterAMessage(string queueName)
    {
        await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

        await SendAMessage(client, queueName, "Hello world!");

        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        // receive the message in peek lock mode
        var receivedMessage = await receiver.ReceiveMessageAsync();

        // dead-letter the message
        await receiver.DeadLetterMessageAsync(receivedMessage);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task ReceiveAndAbandonAMessage(string queueName)
    {
        await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

        await SendAMessage(client, queueName, "Hello world!");

        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

        // receive the message in peek lock mode
        var receivedMessage = await receiver.ReceiveMessageAsync();

        // abandon the message - it'll go back on the queue
        await receiver.AbandonMessageAsync(receivedMessage);

        // receive the message again and complete it to remove it from the queue
        var receivedMessage2 = await receiver.ReceiveMessageAsync();
        await receiver.CompleteMessageAsync(receivedMessage2);        }


    private static async Task SendAMessage(ServiceBusClient client, string queueName, string messageBody)
    {
        await using var sender = client.CreateSender(queueName);
        var message = new ServiceBusMessage(messageBody);
        await sender.SendMessageAsync(message);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public static async Task SendAndReceiveAMessage(string queueName)
    {
        await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

        await SendAMessage(client, queueName, "Hello world!");

        await using var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
        await receiver.ReceiveMessageAsync();
    }
}
