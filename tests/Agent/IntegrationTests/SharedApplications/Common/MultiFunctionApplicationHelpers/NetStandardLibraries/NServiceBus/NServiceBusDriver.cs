// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET462

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
#if NET10_0_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
#endif
using MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Handlers;
using MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Models;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using EventHandler = MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Handlers.EventHandler;
#if NET10_0_OR_GREATER
using IHost = Microsoft.Extensions.Hosting.IHost;
#endif

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus;

[Library]
class NServiceBusDriver
{
    // A queue this endpoint does not consume. SendCommandInTransaction routes the command here
    // (instead of SendLocal) so the produce-side instrumentation is exercised without also
    // producing an incidental no-handler "Consume" transaction on this same endpoint. That
    // incidental transaction has a variable duration and competes with the Send transaction for
    // the single slowest-trace slot per harvest window, which made NsbSendTests flaky. The
    // asserted Produce metric is named from the message type, not the destination queue, so
    // routing the command elsewhere leaves all NsbSendTests assertions unchanged.
    private const string UnconsumedDestinationQueue = "NsbSendTestsUnconsumedQueue";

#if NET10_0_OR_GREATER
    private IHost _host;
#endif
    private IMessageSession _session;


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

#if NET10_0_OR_GREATER
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
        _host = builder.Build();
        _host.StartAsync().Wait();
        _session = _host.Services.GetRequiredService<IMessageSession>();
#else
        _session = Endpoint.Start(endpointConfiguration).Result;
#endif
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
#if NET10_0_OR_GREATER
        _host?.StopAsync().Wait();
        _host?.Dispose();
#else
        ((IEndpointInstance)_session)?.Stop().Wait();
#endif
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
        await _session.Publish(@event);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendCommandInTransaction()
    {
        var command = new Command();
        ConsoleMFLogger.Info($"Sending NServiceBus Command with Id: {command.Id} to {UnconsumedDestinationQueue}");
        await _session.Send(UnconsumedDestinationQueue, command);
    }

    [LibraryMethod]
    public async Task SendCommand()
    {
        var command = new Command();
        ConsoleMFLogger.Info($"Sending NServiceBus Command with Id: {command.Id}");
        await _session.SendLocal(command);
    }
}
#endif
