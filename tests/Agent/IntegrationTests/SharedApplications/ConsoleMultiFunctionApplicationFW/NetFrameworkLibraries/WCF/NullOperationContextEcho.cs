// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.ServiceModel;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Agent.IntegrationTests.Shared.Wcf;
using NewRelic.Api.Agent;

namespace ConsoleMultiFunctionApplicationFW.NetFrameworkLibraries.WCF;

/// <summary>
/// Minimal WCF contract used only by StartServiceWithOffThreadInvoker.
///
/// The shared IWcfService/WcfService contract (used by StartService and every
/// other WCF test) is not suitable for a tight loop of CallCount off-thread
/// invocations: its SyncGetData implementation calls out to
/// https://www.google.com/ on every invocation (WcfService.DoWork), and
/// CallCount rapid-fire calls trip Google's rate limiter (HTTP 429) after the
/// first call. That FaultException is unrelated to the null-OperationContext
/// defect this test targets, but it kills the MFA host process and truncates
/// the run to a single invocation.
///
/// Echo has no dependency on OperationContext.Current, incoming headers,
/// session state, or any external call, so it can run all CallCount
/// invocations from OffThreadOperationInvoker's fresh worker thread with
/// nothing for the thread hop to break.
/// </summary>
[ServiceContract]
public interface INullOperationContextEchoService
{
    [OperationContract]
    string Echo(int value);
}

public class NullOperationContextEchoService : INullOperationContextEchoService
{
    public string Echo(int value)
    {
        return value.ToString();
    }
}

/// <summary>
/// Client exerciser for INullOperationContextEchoService. Mirrors WCFClient's
/// pattern of building the channel once in an Initialize call and reusing it
/// across subsequent LibraryMethod calls.
/// </summary>
[Library]
public class NullOperationContextEchoClient
{
    private INullOperationContextEchoService _channel;

    [LibraryMethod]
    public void InitializeClient(int port, string relativePath)
    {
        var endpointAddress = WCFLibraryHelpers.GetEndpointAddress(WCFBindingType.NetTcp, port, relativePath);
        var binding = new NetTcpBinding { ReceiveTimeout = TimeSpan.FromMinutes(2) };
        var factory = new ChannelFactory<INullOperationContextEchoService>(binding, new EndpointAddress(endpointAddress));
        _channel = factory.CreateChannel();
    }

    [LibraryMethod]
    [Transaction]
    public void Echo(int value)
    {
        if (_channel == null)
        {
            throw new InvalidOperationException("NullOperationContextEchoClient not instantiated");
        }

        var result = _channel.Echo(value);
        ConsoleMFLogger.Info($"NullOperationContextEchoClient.Echo({value}) returned {result}");
    }
}
