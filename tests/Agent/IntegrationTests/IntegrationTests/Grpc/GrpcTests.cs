// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.Grpc;

/// <summary>
/// Tests gRPC client instrumentation using grpc-dotnet (Grpc.Net.Client).
/// Validates metrics and span attributes per:
///   - gRPC spec: https://source.datanerd.us/agents/agent-specs/blob/main/implementation_guides/gRPC.md
///   - OTel RPC Client v1.23: https://source.datanerd.us/agents/agent-specs/blob/main/otel_bridge/Tracing-API.md
/// </summary>
public abstract class GrpcTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;

    protected GrpcTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.KeepWorkingDirectory = true;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
                    .EnableOpenTelemetry(true)
                    .EnableOpenTelemetryMetrics(true)
                    .EnableOpenTelemetryTracing(true)
                    .IncludeActivitySource("Grpc.Net.Client,Grpc.Core,Grpc.AspNetCore.Server")
                    .SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 1);
            }
        );

        // Port 0 = auto-assign a free port
        _fixture.AddCommand("GrpcExerciser SayHello 0");

        _fixture.Initialize();
    }

    [Fact]
    public void Metrics_ExternalCallIsCaptured()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        // These will be unknown until grpc-dotnet fully implements the OTel RPC Client spec and we can get the full server address, service, and method in the metric name.
        // The gRPC spec calls for host and port, but we don't include that in the metric name for any external so skipping that for now.
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"External/all", CallCountAllHarvests = 1 },
            new() { metricName = @"External/allOther", CallCountAllHarvests = 1 },
            new() { metricName = @"External/unknown/all", CallCountAllHarvests = 1 },
            new() { metricName = @"External/unknown/gRPC/greet.Greeter/SayHello", CallCountAllHarvests = 1 },
            new() { metricName = @"External/unknown/gRPC/greet.Greeter/SayHello", metricScope = "OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Grpc.GrpcExerciser/MakeGrpcClientCall", CallCountAllHarvests = 1 },
        };

        NrAssert.Multiple(
            () => Assertions.MetricsExist(expectedMetrics, metrics)
        );
    }

    [Fact]
    public void SpanEvents_HaveGrpcAttributes()
    {
        var spanEvents = _fixture.AgentLog.GetSpanEvents().ToList();

        // Find span events for the external gRPC call.
        // These should have category "http" (external call category) or contain gRPC attributes.
        var externalSpanEvent = spanEvents
            .FirstOrDefault(e =>
                e.IntrinsicAttributes.TryGetValue("category", out var cat) && cat.Equals("http")
                && e.IntrinsicAttributes.TryGetValue("component", out var comp) && comp.Equals("grpc"));

        // checks if category and component attributes are correctly set on the span event
        Assert.NotNull(externalSpanEvent);

        var expectedInstrinsicAttributes = new Dictionary<string, object>
        {
            { "category", "http" },
            { "component", "grpc" },
            { "server.address", "unknown" },
            { "server.port", 0 },
            { "span.kind", "client" },
        };

        var expectedAgentAttributes = new Dictionary<string, object>
        {
            { "http.url", "grpc://unknown:0/greet.Greeter/SayHello" },
            { "http.method", "greet.Greeter/SayHello" }, //TODO gRPC spec says this is the correct "method" attribute
            { "http.request.method", "greet.Greeter/SayHello" }, //TODO Otel spec says this should be "procedure", but externals use this
            { "procedure", "greet.Greeter/SayHello" }, // TODO Otel spec says this is the correct "method" attribute
            // { "grpc.statusCode", 0 }, //TODO Not present in grpc-dotnet v2.50.0; Per Otel spec converted from rpc.grpc.status_code, but not in OTel semantic conventions for RPC
            { "otel.instrumentation_library.name", "Grpc.Net.Client" },
            { "otel.scope.name", "Grpc.Net.Client" },
            { "status.code", "unset" }, // Activity status code

        };

        Assertions.SpanEventHasAttributes(expectedInstrinsicAttributes, SpanEventAttributeType.Intrinsic, externalSpanEvent);
        NrAssert.Multiple(
            () => Assertions.SpanEventHasAttributes(expectedInstrinsicAttributes, SpanEventAttributeType.Intrinsic, externalSpanEvent),
            () => Assertions.SpanEventHasAttributes(expectedAgentAttributes, SpanEventAttributeType.Agent, externalSpanEvent)
        );
    }

    [Fact]
    public void NoWrapperErrors()
    {
        var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
        var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);
        Assert.Null(wrapperError);
    }
}

public class GrpcTests_NetCoreOldest : GrpcTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    public GrpcTests_NetCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class GrpcTests_NetCoreLatest : GrpcTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    public GrpcTests_NetCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
