// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#if !NET462

namespace NsbTests
{
    [Library]
    class NServiceBusDriver
    {
        private IEndpointInstance _endpoint;


        private void StartNServiceBusInternal(Type handlerToAllow = null)
        {
            ConsoleMFLogger.Info($"Starting NServiceBus");
            if (handlerToAllow != null)
            {
                ConsoleMFLogger.Info($"Enabling handler: {handlerToAllow.Name}");
            }

            var endpointConfiguration = new EndpointConfiguration("NRSubscriber");
            endpointConfiguration.UsePersistence<LearningPersistence>();
            var transport = endpointConfiguration.UseTransport<LearningTransport>();
            transport.StorageDirectory(".lt");
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.EnableInstallers();

#if NET8_0_OR_GREATER // serializer must be specified starting with NServiceBus 9.0.0
            endpointConfiguration.UseSerialization<XmlSerializer>();
#endif

            // We want to control which handlers are loaded for different test cases
            endpointConfiguration.AssemblyScanner().ScanAppDomainAssemblies = false;
            var typesToIgnore = new List<Type>
            {
                typeof(CommandHandler),
                typeof(ThrowingCommandHandler),
                typeof(AsyncCommandHandler),
                typeof(EventHandler),
                typeof(AsyncEventHandler)
            };
            typesToIgnore.Remove(handlerToAllow);
            endpointConfiguration.AssemblyScanner().ExcludeTypes(typesToIgnore.ToArray());

            // Disable retry for failure tests
            endpointConfiguration.Recoverability().Immediate(
                immediate =>
                {
                    immediate.NumberOfRetries(0);
                });

            _endpoint = Endpoint.Start(endpointConfiguration).Result;
        }

        [LibraryMethod]
        public void StartNServiceBusWithoutHandlers()
        {
            StartNServiceBusInternal();
        }

        [LibraryMethod]
        public void StartNServiceBusWithCommandHandler()
        {
            StartNServiceBusInternal(typeof(CommandHandler));
        }

        [LibraryMethod]
        public void StartNServiceBusWithAsyncCommandHandler()
        {
            StartNServiceBusInternal(typeof(AsyncCommandHandler));
        }

        [LibraryMethod]
        public void StartNServiceBusWithThrowingCommandHandler()
        {
            StartNServiceBusInternal(typeof(ThrowingCommandHandler));
        }

        [LibraryMethod]
        public void StartNServiceBusWithEventHandler()
        {
            StartNServiceBusInternal(typeof(EventHandler));
        }

        [LibraryMethod]
        public void StartNServiceBusWithAsyncEventHandler()
        {
            StartNServiceBusInternal(typeof(AsyncEventHandler));
        }

        [LibraryMethod]
        public void StopNServiceBus()
        {
            ConsoleMFLogger.Info($"Stopping NServiceBus");
            _endpoint?.Stop().Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task PublishEventInTransaction()
        {
            await PublishEvent();
        }

        [LibraryMethod]
        public async Task PublishEvent()
        {
            var @event = new Event();
            ConsoleMFLogger.Info($"Sending NServiceBus Event with Id: {@event.Id}");
            await _endpoint.Publish(@event);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task SendCommandInTransaction()
        {
            await SendCommand();
        }

        [LibraryMethod]
        public async Task SendCommand()
        {
            var command = new Command();
            ConsoleMFLogger.Info($"Sending NServiceBus Command with Id: {command.Id}");
            await _endpoint.SendLocal(command);
        }
    }
}

#endif
