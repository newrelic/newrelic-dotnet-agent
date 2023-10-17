// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET7_0

using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        Task _hostedServiceTask;
        CancellationTokenSource _cts;
        IHost _host;
        IBus _bus;

        [LibraryMethod]
        public void StartHost()
        {
            _host = CreateMassTransitHost();
            _bus = _host.Services.GetService<IBus>();
            _cts = new CancellationTokenSource();
            _hostedServiceTask = _host.RunAsync(_cts.Token);
        }

        [LibraryMethod]
        public void StopHost()
        {
            _cts.Cancel();
            _hostedServiceTask.Wait();
            //while (! _hostedServiceTask.IsCanceled)
            //{
            //    ConsoleMFLogger.Info("Waiting for cancellation");
            //    Thread.Sleep(1000);
            //}
            _hostedServiceTask.Dispose();
        }

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

        private static IHost CreateMassTransitHost()
        {
            var builder = Host.CreateDefaultBuilder().ConfigureServices((hostContext, services) =>
                {
                    services.AddMassTransit(x =>
                    {
                        //x.SetKebabCaseEndpointNameFormatter();

                        // By default, sagas are in-memory, but should be changed to a durable
                        // saga repository.
                        //x.SetInMemorySagaRepositoryProvider();

                        //var entryAssembly = Assembly.GetEntryAssembly();

                        x.AddConsumer<MessageConsumer>();
                        //x.AddConsumers(entryAssembly);
                        //x.AddSagaStateMachines(entryAssembly);
                        //x.AddSagas(entryAssembly);
                        //x.AddActivities(entryAssembly);

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
#endif
