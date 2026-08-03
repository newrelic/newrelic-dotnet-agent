// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.DistributedTracing;

/// <summary>
/// Verifies that the agent injects distributed-trace headers on HttpWebRequest requests that
/// send a body (POST/PUT). These requests obtain the request stream before calling GetResponse,
/// so without GetRequestStream-side injection the headers were serialized before the external
/// segment existed and never went out. The receiver recording a parentId (and the
/// TraceContext/Accept/Success metric) proves the headers arrived. Covers the synchronous
/// GetRequestStream path and the asynchronous GetRequestStreamAsync (TAP) and
/// BeginGetRequestStream (APM) paths.
/// </summary>
public abstract class HttpWebRequestBodyDTTestsBase : NewRelicIntegrationTest<FrameworkTracingChainFixture>
{
    private readonly FrameworkTracingChainFixture _fixture;

    protected HttpWebRequestBodyDTTestsBase(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetOrDeleteSpanEventsEnabled(true);
                configModifier.SetOrDeleteDistributedTraceEnabled(true);
                configModifier.SetLogLevel("debug");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);

                var environmentVariables = new Dictionary<string, string>();

                _fixture.ReceiverApplication = _fixture.SetupReceiverApplication(isDistributedTracing: true, isWebApplication: true);
                _fixture.ReceiverApplication.Start(string.Empty, environmentVariables, captureStandardOutput: true);
            },
            exerciseApplication: () =>
            {
                ExerciseApplication(_fixture);

                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.AnalyticsEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.SpanEventDataLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.ReceiverAppAgentLog.WaitForLogLine(AgentLogFile.MetricDataLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    protected abstract void ExerciseApplication(FrameworkTracingChainFixture fixture);

    [Fact]
    public void ReceiverAcceptsDistributedTraceFromBodyRequest()
    {
        var receiverMetrics = _fixture.ReceiverAppAgentLog.GetMetrics().ToArray();

        var receiverExpectedMetrics = new List<Assertions.ExpectedMetric>
        {
            new() { metricName = @"Supportability/TraceContext/Accept/Success", CallCountAllHarvests = 1 },
        };

        Assertions.MetricsExist(receiverExpectedMetrics, receiverMetrics);

        var receiverTxEvent = _fixture.ReceiverAppAgentLog.GetTransactionEvents().FirstOrDefault();
        Assert.NotNull(receiverTxEvent);
        Assert.True(receiverTxEvent.IntrinsicAttributes.ContainsKey("parentId"),
            "Receiver should have parentId from DT headers injected on the HttpWebRequest body request");
    }
}

public class HttpWebRequestPostSyncDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestPostSyncDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodySync("POST");
}

public class HttpWebRequestPutSyncDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestPutSyncDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodySync("PUT");
}

public class HttpWebRequestPostAsyncDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestPostAsyncDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodyAsync("POST", "tap");
}

public class HttpWebRequestPutAsyncDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestPutAsyncDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodyAsync("PUT", "tap");
}

public class HttpWebRequestPostBeginEndDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestPostBeginEndDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodyAsync("POST", "apm");
}

// Bodyless asynchronous GET via GetResponseAsync. Unlike the body cases, there is no request
// stream and therefore no handoff for the response wrapper to reuse, so this exercises whether the
// async GET path injects distributed-trace headers at all. Asserts the same receiver-side outcome
// as the body cases (Accept/Success + parentId).
public class HttpWebRequestGetAsyncDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestGetAsyncDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestAsync();
}

// POST body request whose response is an HTTP 500, so GetResponse throws WebException and the
// external segment's onFailure path runs. The request still carries the distributed-trace headers,
// so the receiver records the trace (Accept/Success + parentId).
public class HttpWebRequestBodyErrorDTTests : HttpWebRequestBodyDTTestsBase
{
    public HttpWebRequestBodyErrorDTTests(FrameworkTracingChainFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }

    protected override void ExerciseApplication(FrameworkTracingChainFixture fixture) =>
        fixture.ExecuteTraceRequestChainHttpWebRequestBodyError();
}
