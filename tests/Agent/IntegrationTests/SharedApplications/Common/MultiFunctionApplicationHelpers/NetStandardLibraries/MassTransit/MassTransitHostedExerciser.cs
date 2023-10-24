// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MultiFunctionApplicationHelpers.NetStandardLibraries.MassTransit;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using IHost = Microsoft.Extensions.Hosting.IHost;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries
{
    [Library]
    class MassTransitHostedExerciser
    {
        Task _hostedServiceTask;
        CancellationTokenSource _cts;
        IHost _host;
        //IBus _bus;

        [LibraryMethod]
        public void StartHost()
        {
            _host = CreateMassTransitHost();
            _cts = new CancellationTokenSource();
            _hostedServiceTask = _host.RunAsync(_cts.Token);
        }

        [LibraryMethod]
        public void StopHost()
        {
            _cts.Cancel();
            _hostedServiceTask.Wait();
            _hostedServiceTask.Dispose();
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Publish(string text)
        {
            var message = new Message() { Text = text };
            var bus = _host.Services.GetService<IBus>();
            await bus.Publish(message);
            ConsoleMFLogger.Info($"Sent message {text}");

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);
        }

        [LibraryMethod]
        [Transaction]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public async Task Send(string text)
        {
            var message = new Message() { Text = text };
            var bus = _host.Services.GetService<IBus>();
            var sendEndpoint = await bus.GetPublishSendEndpoint<Message>();
            await sendEndpoint.Send(message);
            ConsoleMFLogger.Info($"Sent message {text}");

            // This sleep ensures that this transaction method is the one sampled for transaction trace data
            Thread.Sleep(1000);
        }
        private static IHost CreateMassTransitHost()
        {
            var builder = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
            {
                services.AddMassTransit(x =>
                {
                    x.SetKebabCaseEndpointNameFormatter();

                    // By default, sagas are in-memory, but should be changed to a durable
                    // saga repository.
                    x.SetInMemorySagaRepositoryProvider();
                    var entryAssembly = Assembly.GetEntryAssembly();

                    //x.AddConsumers(entryAssembly);
                    x.AddSagaStateMachines(entryAssembly);
                    x.AddSagas(entryAssembly);
                    x.AddActivities(entryAssembly);


                    x.AddConsumer<MessageConsumer>();

                    x.UsingInMemory((context, cfg) =>
                    {
                        cfg.ConfigureEndpoints(context);
                    });
                });
            });
            return builder.Build();
        }

    }
}
