// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using System;
using System.Messaging;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NServiceBus;
using NServiceBus.Logging;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    [Library]
    public class NServiceBusReceiverHost
    {
        private static IBus _bus;
        private static string _endpointName = "NServiceBusReceiverHost";
        private static string _baseQueueName = $@"{Environment.MachineName}\private$\{_endpointName}";
        private static string _retriesQueueName = $@"{Environment.MachineName}\private$\{_endpointName}.retries";
        private static string _timeoutsQueueName = $@"{Environment.MachineName}\private$\{_endpointName}.timeouts";
        private static string _timeoutsdispatcherQueueName = $@"{Environment.MachineName}\private$\{_endpointName}.timeoutsdispatcher";

        [LibraryMethod]
        public void Start()
        {
            Logger.Info($"Starting NServiceBusReceiverHost");
            SetupQueues();

            var defaultFactory = LogManager.Use<DefaultFactory>();
            defaultFactory.Level(LogLevel.Error);

            var busConfig = new BusConfiguration();
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.UseTransport<MsmqTransport>();
            busConfig.DiscardFailedMessagesInsteadOfSendingToErrorQueue();
            busConfig.EndpointName(_endpointName);
            var startableBus = Bus.Create(busConfig);
            _bus = startableBus.Start();
            Logger.Info($"NServiceBusReceiverHost Started");
        }

        [LibraryMethod]
        public void Stop()
        {
            Logger.Info($"Stopping NServiceBusReceiverHost");
            _bus.Dispose();
            Logger.Info($"NServiceBusReceiverHost Stopped");
        }

        private void SetupQueues()
        {
            if (!MessageQueue.Exists(_baseQueueName))
            {
                var messageQueue = MessageQueue.Create(_baseQueueName, true);
                messageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
            }

            if (!MessageQueue.Exists(_retriesQueueName))
            {
                var messageQueue = MessageQueue.Create(_retriesQueueName, true);
                messageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
            }

            if (!MessageQueue.Exists(_timeoutsQueueName))
            {
                var messageQueue = MessageQueue.Create(_timeoutsQueueName, true);
                messageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
            }

            if (!MessageQueue.Exists(_timeoutsdispatcherQueueName))
            {
                var messageQueue = MessageQueue.Create(_timeoutsdispatcherQueueName, true);
                messageQueue.SetPermissions("Everyone", MessageQueueAccessRights.FullControl);
            }
        }
    }
}

#endif
