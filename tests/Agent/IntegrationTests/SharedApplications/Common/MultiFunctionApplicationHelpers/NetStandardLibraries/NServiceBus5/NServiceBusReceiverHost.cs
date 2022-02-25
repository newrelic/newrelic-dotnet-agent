// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


#if NET462

using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NServiceBus;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus5
{
    [Library]
    public class NServiceBusReceiverHost
    {
        private static IBus _bus;
        private static string _endpointName = "NServiceBusReceiverHost";

        [LibraryMethod]
        public void Start()
        {
            ConsoleMFLogger.Info($"Starting NServiceBusReceiverHost");
            var busConfig = new BusConfiguration();
            busConfig.UsePersistence<InMemoryPersistence>();
            busConfig.UseTransport<MsmqTransport>();
            busConfig.CustomConfigurationSource(new ConfigurationSource());
            busConfig.LoadMessageHandlers<First<MessageHandler>>();
            busConfig.EndpointName(_endpointName);
            var startableBus = Bus.Create(busConfig);
            _bus = startableBus.Start();
            ConsoleMFLogger.Info($"NServiceBusReceiverHost Started");
        }

        [LibraryMethod]
        public void Stop()
        {
            ConsoleMFLogger.Info($"Stopping NServiceBusReceiverHost");
            _bus.Dispose();
            ConsoleMFLogger.Info($"NServiceBusReceiverHost Stopped");
        }
    }
}

#endif
