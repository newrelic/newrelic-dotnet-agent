// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.AzureServiceBus
{
    [Library]
    internal class AzureServiceBusExerciser
    {
        private const string Subscription = "test";

        #region Queue

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
        public static async Task SendAndReceiveAMessageForQueue(string queueName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);
            await SendAMessage(client, queueName, "Hello world!");

            await using var receiver = client.CreateReceiver(queueName,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
            await receiver.ReceiveMessageAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ExerciseMultipleReceiveOperationsOnAMessageForQueue(string queueName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            await SendAMessage(client, queueName, "Hello world!");

            await using var receiver = client.CreateReceiver(queueName,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

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
        public static async Task ReceiveAndDeadLetterAMessageForQueue(string queueName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            await SendAMessage(client, queueName, "Hello world!");

            await using var receiver = client.CreateReceiver(queueName,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

            // receive the message in peek lock mode
            var receivedMessage = await receiver.ReceiveMessageAsync();

            // dead-letter the message
            await receiver.DeadLetterMessageAsync(receivedMessage);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ReceiveAndAbandonAMessageForQueue(string queueName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            await SendAMessage(client, queueName, "Hello world!");

            await using var receiver = client.CreateReceiver(queueName,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

            // receive the message in peek lock mode
            var receivedMessage = await receiver.ReceiveMessageAsync();

            // abandon the message - it'll go back on the queue
            await receiver.AbandonMessageAsync(receivedMessage);

            // receive the message again and complete it to remove it from the queue
            var receivedMessage2 = await receiver.ReceiveMessageAsync();
            await receiver.CompleteMessageAsync(receivedMessage2);
        }

        [LibraryMethod]
        // [Transaction] no transaction on this one; we're testing that the ServiceBusProcessor is creating transactions
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ExerciseServiceBusProcessorForQueue(string queueName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            // create the sender
            await using ServiceBusSender sender = client.CreateSender(queueName);

            // create a set of messages that we can send
            ServiceBusMessage[] messages = new ServiceBusMessage[]
            {
                new ServiceBusMessage("First"),
                new ServiceBusMessage("Second")
            };

            // send the message batch
            await sender.SendMessagesAsync(messages);

            // create the options to use for configuring the processor
            ServiceBusProcessorOptions options = new()
            {
                MaxConcurrentCalls = 1 // multi-threading. Yay!
            };

            // create a processor that we can use to process the messages
            await using ServiceBusProcessor processor = client.CreateProcessor(queueName, options);

            var receivedMessages = 0;

            // configure the message handler to use
            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler; // ErrorHandler is required, but we won't exercise it

            async Task MessageHandler(ProcessMessageEventArgs args)
            {
                string body = args.Message.Body.ToString();
                var threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"ThreadId: {threadId} - body: {body}");

                Interlocked.Increment(ref receivedMessages);

                await Task.Delay(1000); // simulate processing the message
            }

            Task ErrorHandler(ProcessErrorEventArgs args)
            {
                // the error source tells me at what point in the processing an error occurred
                Console.WriteLine(args.ErrorSource);
                // the fully qualified namespace is available
                Console.WriteLine(args.FullyQualifiedNamespace);
                // as well as the entity path
                Console.WriteLine(args.EntityPath);
                Console.WriteLine(args.Exception.ToString());
                return Task.CompletedTask;
            }

            // start processing
            await processor.StartProcessingAsync();

            // wait up to 30 seconds or until receivedMessages has a count of 2
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (receivedMessages < 2 && DateTime.UtcNow < timeout)
            {
                await Task.Delay(1000);
            }

            // chill for a bit
            await Task.Delay(TimeSpan.FromSeconds(5));

            // stop processing
            await processor.StopProcessingAsync();
        }

        #endregion Queue

        #region Topic

        [LibraryMethod]
        public static async Task InitializeTopic(string topicName)
        {
            var adminClient = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);
            // if the topic exists, delete it and re-create it
            if (await adminClient.TopicExistsAsync(topicName))
            {
                await adminClient.DeleteTopicAsync(topicName);
            }

            await adminClient.CreateTopicAsync(topicName);
            await adminClient.CreateSubscriptionAsync(topicName, Subscription);
        }

        [LibraryMethod]
        public static async Task DeleteTopic(string topicName)
        {
            var adminClient = new ServiceBusAdministrationClient(AzureServiceBusConfiguration.ConnectionString);
            if (await adminClient.TopicExistsAsync(topicName))
            {
                await adminClient.DeleteTopicAsync(topicName);
            }
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task SendAndReceiveAMessageForTopic(string topicName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);
            await SendAMessage(client, topicName, "Hello world!");

            await using var receiver = client.CreateReceiver(topicName, Subscription,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete });
            await receiver.ReceiveMessageAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ExerciseMultipleReceiveOperationsOnAMessageForTopic(string topicName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            await SendAMessage(client, topicName, "Hello world!");

            await using var receiver = client.CreateReceiver(topicName, Subscription,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

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
        public static async Task ReceiveAndAbandonAMessageForTopic(string topicName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            await SendAMessage(client, topicName, "Hello world!");

            await using var receiver = client.CreateReceiver(topicName, Subscription,
                new ServiceBusReceiverOptions() { ReceiveMode = ServiceBusReceiveMode.PeekLock });

            // receive the message in peek lock mode
            var receivedMessage = await receiver.ReceiveMessageAsync();

            // abandon the message - it'll go back on the queue
            await receiver.AbandonMessageAsync(receivedMessage);

            // receive the message again and complete it to remove it from the queue
            var receivedMessage2 = await receiver.ReceiveMessageAsync();
            await receiver.CompleteMessageAsync(receivedMessage2);
        }

        [LibraryMethod]
        // [Transaction] no transaction on this one; we're testing that the ServiceBusProcessor is creating transactions
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ExerciseServiceBusProcessorForTopic(string topicName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);

            // create the sender
            await using ServiceBusSender sender = client.CreateSender(topicName);

            // create a set of messages that we can send
            ServiceBusMessage[] messages = new ServiceBusMessage[]
            {
                new ServiceBusMessage("First"), new ServiceBusMessage("Second")
            };

            // send the message batch
            await sender.SendMessagesAsync(messages);

            // create the options to use for configuring the processor
            ServiceBusProcessorOptions options = new()
            {
                MaxConcurrentCalls = 1 // multi-threading. Yay!
            };

            // create a processor that we can use to process the messages
            await using ServiceBusProcessor processor = client.CreateProcessor(topicName, Subscription, options);

            var receivedMessages = 0;

            // configure the message handler to use
            processor.ProcessMessageAsync += MessageHandler;
            processor.ProcessErrorAsync += ErrorHandler; // ErrorHandler is required, but we won't exercise it

            async Task MessageHandler(ProcessMessageEventArgs args)
            {
                string body = args.Message.Body.ToString();
                var threadId = Thread.CurrentThread.ManagedThreadId;
                Console.WriteLine($"ThreadId: {threadId} - body: {body}");

                Interlocked.Increment(ref receivedMessages);

                await Task.Delay(1000); // simulate processing the message
            }

            Task ErrorHandler(ProcessErrorEventArgs args)
            {
                // the error source tells me at what point in the processing an error occurred
                Console.WriteLine(args.ErrorSource);
                // the fully qualified namespace is available
                Console.WriteLine(args.FullyQualifiedNamespace);
                // as well as the entity path
                Console.WriteLine(args.EntityPath);
                Console.WriteLine(args.Exception.ToString());
                return Task.CompletedTask;
            }

            // start processing
            await processor.StartProcessingAsync();

            // wait up to 30 seconds or until receivedMessages has a count of 2
            var timeout = DateTime.UtcNow.AddSeconds(30);
            while (receivedMessages < 2 && DateTime.UtcNow < timeout)
            {
                await Task.Delay(1000);
            }

            // chill for a bit
            await Task.Delay(TimeSpan.FromSeconds(5));

            // stop processing
            await processor.StopProcessingAsync();
        }

        #endregion Topic

        #region Queue and Topic

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static async Task ScheduleAndCancelAMessage(string queueOrTopicName)
        {
            await using var client = new ServiceBusClient(AzureServiceBusConfiguration.ConnectionString);
            await using var sender = client.CreateSender(queueOrTopicName);

            var message = new ServiceBusMessage("Hello world!");
            var messageSequenceId = await sender.ScheduleMessageAsync(message, DateTime.UtcNow.AddSeconds(90));

            // cancel the scheduled message
            await sender.CancelScheduledMessageAsync(messageSequenceId);
        }

        #endregion Queue and Topic

        // The same method sends a message to either a queue or a topic.
        private static async Task SendAMessage(ServiceBusClient client, string queueOrTopicName, string messageBody)
        {
            await using var sender = client.CreateSender(queueOrTopicName);
            var message = new ServiceBusMessage(messageBody);
            await sender.SendMessageAsync(message);
        }
    }
}
