// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET481_OR_GREATER && !NET7_0_OR_GREATER
#define MASSTRANSIT7
#endif

using MassTransit;
using MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using IHost = Microsoft.Extensions.Hosting.IHost;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class MassTransitExerciser
    {
        CancellationTokenSource _cts;
        Task _hostedServiceTask;
        IHost _host;
        IBus _bus;
        IBusControl _busControl;

// Note that StartHost/StopHost and StartBus/StopBus are two different
// setup methods

        [LibraryMethod]
        public void StartHost()
        {
            _host = CreateMassTransitHost();
            _bus = _host.Services.GetService<IBus>();
            _cts = new CancellationTokenSource();
            _hostedServiceTask = _host.RunAsync(_cts.Token);
        }

        private static IHost CreateMassTransitHost()
        {
            var builder = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
            {
                services.AddMassTransit(x =>
                {
                    x.AddConsumer<MessageConsumer>();

                    x.UsingInMemory((context, cfg) =>
                    {
                        cfg.ConfigureEndpoints(context);
                    });

                });
#if MASSTRANSIT7
                services.AddMassTransitHostedService();
#endif
            });
            return builder.Build();
        }

        [LibraryMethod]
        public void StopHost()
        {
            _cts.Cancel();
            _hostedServiceTask.Wait();
            _hostedServiceTask.Dispose();
        }

        [LibraryMethod]
        public async Task StartBus()
        {
            _cts = new CancellationTokenSource();
            _busControl = Bus.Factory.CreateUsingInMemory(configure =>
            {
                configure.ReceiveEndpoint(System.Guid.NewGuid().ToString(), cfg =>
                {
                    cfg.Consumer<MessageConsumer>();
                });
            });

            // IBusControl is an IBus and this lets the Publish and Send methods not care which
            // setup method was used
            _bus = _busControl;

            await _busControl.StartAsync(_cts.Token);
        }

        [LibraryMethod]
        public async Task StopBus()
        {
            await _busControl.StopAsync();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Publish(string text)
        {
            var message = new Message() { Text = text };
            await _bus.Publish(message);
            ConsoleMFLogger.Info($"Published message {text}");

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Send(string text)
        {
            var message = new Message() { Text = text };
            var sendEndpoint = await _bus.GetPublishSendEndpoint<Message>();
            await sendEndpoint.Send(message);
            ConsoleMFLogger.Info($"Sent message {text}");

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);
        }
    }
}
