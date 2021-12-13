// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using NServiceBusTests;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if !NET462

namespace NServiceBusTests
{
    [Library]
    class NServiceBusDriver
    {
        private IEndpointInstance _endpoint;

        
        private void StartNServiceBusInternal(Type handlerToAllow = null)
        {
            Logger.Info($"Starting NServiceBus");
            if(handlerToAllow != null)
            {
                Logger.Info($"Enabling handler: {handlerToAllow.Name}");
            }

            var endpointConfiguration = new EndpointConfiguration("NRSubscriber");
            endpointConfiguration.UsePersistence<LearningPersistence>();
            var transport = endpointConfiguration.UseTransport<LearningTransport>();
            transport.StorageDirectory(".lt");
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.EnableInstallers();

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
        public void StartNServiceBusWithEventHandler()
        {
            StartNServiceBusInternal(typeof(EventHandler));
        }

        [LibraryMethod]
        public void StartNServiceBusWithAsyncEventHandler()
        {
            StartNServiceBusInternal(typeof(EventHandler));
        }

        [LibraryMethod]
        public void StopNServiceBus()
        {
            Logger.Info($"Stopping NServiceBus");
            _endpoint?.Stop().Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void PublishEventInTransaction()
        {
            PublishEvent();
        }

        [LibraryMethod]
        public void PublishEvent()
        {
            var @event = new Event();
            Logger.Info($"Sending NServiceBus Event with Id: {@event.Id}");
            _endpoint.Publish(@event).Wait();
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendCommandInTransaction()
        {
            SendCommand();
        }

        [LibraryMethod]
        public void SendCommand()
        {
            var command = new Command();
            Logger.Info($"Sending NServiceBus Command with Id: {command.Id}");
            _endpoint.SendLocal(command).Wait();
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }
    }
}

#endif
