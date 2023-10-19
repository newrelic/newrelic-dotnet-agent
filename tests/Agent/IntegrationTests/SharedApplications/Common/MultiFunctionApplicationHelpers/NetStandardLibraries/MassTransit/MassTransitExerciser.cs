// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MassTransit;
using MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class MassTransitExerciser
    {
        CancellationTokenSource _cts;
        IBusControl _bus;

        [LibraryMethod]
        public async Task StartBus(string queueName)
        {
            _cts = new CancellationTokenSource();
            _bus = Bus.Factory.CreateUsingInMemory(configure =>
            {
                configure.ReceiveEndpoint(queueName, cfg =>
                {
                    cfg.Consumer<MessageConsumer>();
                });
            });
            await _bus.StartAsync(_cts.Token);
        }

        [LibraryMethod]
        public async Task StopBus()
        {
            await _bus.StopAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Publish(string text)
        {
            var message = new Message() { Text = text };
            await _bus.Publish(message);
            ConsoleMFLogger.Info($"Sent message {text}");

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);
        }

    }
}
