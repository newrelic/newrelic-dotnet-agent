// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using NewRelic.Api.Agent;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NServiceBus;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    [Library]
    public class NServiceBusService
    {
        private const string DestinationReceiverHost = "NServiceBusReceiverHost";
        private static IBus _bus;
        private static Random _random;

        [LibraryMethod]
        public void Start()
        {
            ConsoleMFLogger.Info($"Starting NServiceBusService");
            var busConfig = new BusConfiguration();
            busConfig.UsePersistence<InMemoryPersistence>();
            var typeToScan = new List<Type>
            {
                typeof(SampleNServiceBusMessage),
                typeof(SampleNServiceBusMessage2)
            };
            busConfig.TypesToScan(typeToScan);
            busConfig.CustomConfigurationSource(new ConfigurationSource());
            _bus = NServiceBus.Bus.Create(busConfig);
            _random = new Random();
            ConsoleMFLogger.Info($"NServiceBusService Started");
        }

        [LibraryMethod]
        public void Stop()
        {
            ConsoleMFLogger.Info($"Stopping NServiceBusService");
            _bus.Dispose();
            ConsoleMFLogger.Info($"NServiceBusService Stopped");
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public void Send()
        {
            var message = new SampleNServiceBusMessage(_random.Next(), "Foo bar");
            _bus.Send(DestinationReceiverHost, message);
            ConsoleMFLogger.Info($"Message with ID={message.Id} sent via NServiceBus" );
        }

        [LibraryMethod]
        public void SendValid()
        {
            var message = new SampleNServiceBusMessage2(_random.Next(), "Valid");
            _bus.Send(DestinationReceiverHost, message);
            ConsoleMFLogger.Info($"Message with ID={message.Id} sent via NServiceBus");
        }

        [LibraryMethod]
        public void SendInvalid()
        {
            var message = new SampleNServiceBusMessage2(_random.Next(), "Invalid", false);
            _bus.Send(DestinationReceiverHost, message);
            ConsoleMFLogger.Info($"Message with ID={message.Id} sent via NServiceBus");
        }
    }
}

#endif
