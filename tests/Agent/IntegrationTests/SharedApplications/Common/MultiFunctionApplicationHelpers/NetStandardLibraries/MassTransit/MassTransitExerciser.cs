// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MassTransit;
using MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
#if NET7_0_OR_GREATER
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
#endif

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class MassTransitExerciser
    {
        CancellationTokenSource _cts;
#if NET7_0_OR_GREATER
        Task _hostedServiceTask;
        IHost _host;
        IBus _bus;
#else
        IBusControl _bus;
#endif

// Note that StartHost/Stophost and StartBus/StopBus are two different
// setup methods
#if NET7_0_OR_GREATER
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
#else
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
#endif

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Publish(string message)
        {
            var order = new Message() { Text = message };
            await _bus.Publish(order);
            ConsoleMFLogger.Info($"Sent message {message}");

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
