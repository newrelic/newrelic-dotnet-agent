// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !NET462

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Handlers;
using MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Models;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using NServiceBus;
using EventHandler = MultiFunctionApplicationHelpers.NetStandardLibraries.NServiceBus.Handlers.EventHandler;

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

    // NServiceBus 10.2 deprecated the self-hosting API (IEndpointInstance, Endpoint.Start/Stop) in favor
    // of an IHostApplicationBuilder-based host with IServiceCollection.AddNServiceBusEndpoint. These MFA
    // console apps have no generic host and only start an endpoint on demand when a test exercises
    // NServiceBus, so migrating to the hosted model isn't practical here. Suppress the obsolete warnings
    // (promoted to errors by TreatWarningsAsErrors) at each usage site until self-hosting is removed in
    // NServiceBus 12. TODO: revisit before upgrading past NServiceBus 11.
#pragma warning disable CS0618 // Type or member is obsolete
    private IEndpointInstance _endpoint;
#pragma warning restore CS0618 // Type or member is obsolete


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

#pragma warning disable CS0618 // Type or member is obsolete - self-hosting deprecated in NServiceBus 10.2
        _endpoint = Endpoint.Start(endpointConfiguration).Result;
#pragma warning restore CS0618 // Type or member is obsolete
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
#pragma warning disable CS0618 // Type or member is obsolete - self-hosting deprecated in NServiceBus 10.2
        _endpoint?.Stop().Wait();
#pragma warning restore CS0618 // Type or member is obsolete
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
        await _endpoint.Publish(@event);
    }

    [LibraryMethod]
    [Transaction]
    [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
    public async Task SendCommandInTransaction()
    {
        var command = new Command();
        ConsoleMFLogger.Info($"Sending NServiceBus Command with Id: {command.Id} to {UnconsumedDestinationQueue}");
        await _endpoint.Send(UnconsumedDestinationQueue, command);
    }

    [LibraryMethod]
    public async Task SendCommand()
    {
        var command = new Command();
        ConsoleMFLogger.Info($"Sending NServiceBus Command with Id: {command.Id}");
        await _endpoint.SendLocal(command);
    }
}
#endif
