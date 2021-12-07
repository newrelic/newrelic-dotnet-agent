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

        [LibraryMethod]
        public void StartNServiceBus()
        {
            Logger.Info($"Starting NServiceBus");
            var endpointConfiguration = new EndpointConfiguration("NRSubscriber");
            endpointConfiguration.UsePersistence<LearningPersistence>();
            var transport = endpointConfiguration.UseTransport<LearningTransport>();
            transport.StorageDirectory(".lt");
            endpointConfiguration.SendFailedMessagesTo("error");
            endpointConfiguration.EnableInstallers();

            _endpoint = Endpoint.Start(endpointConfiguration).Result;
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
        public void SendCommand()
        {
            var command = new Command();
            Logger.Info($"Sending NServiceBus Command with Id: {command.Id}");
            _endpoint.SendLocal(command).Wait();
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void PublishMessage()
        {
            var message = new Message();
            Logger.Info($"Publishing NServiceBus Message with Id: {message.Id}");
            _endpoint.Publish(message).Wait();
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void SendMessage()
        {
            var message = new Message();
            Logger.Info($"Sending NServiceBus Message with Id: {message.Id}");
            
            _endpoint.SendLocal(message).Wait();
            Task.Delay(TimeSpan.FromSeconds(2)).Wait();
        }

    }
}

#endif
