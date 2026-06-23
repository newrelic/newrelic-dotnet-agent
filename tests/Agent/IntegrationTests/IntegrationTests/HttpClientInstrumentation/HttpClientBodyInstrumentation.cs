// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.HttpClientInstrumentation;

// Exercises HttpClient requests that send a body (POST/PUT). On .NET Framework HttpClient's handler
// is layered over HttpWebRequest, so the body goes out through HttpWebRequest.GetRequestStream and
// the response through GetResponse - the methods the HttpWebRequest body wrappers instrument.
// HttpClient owns the external segment and DT header injection, so those wrappers must defer to it.
// The assertions below require exactly one external segment per call (External/all == 2, exactly two
// http spans, one Stream/POST and one Stream/PUT) and that the segment is owned by the HttpClient
// instrumentation - so a duplicate segment from the HttpWebRequest body path would fail the test.
public abstract class HttpClientBodyInstrumentationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : ConsoleDynamicMethodFixture
{
    private readonly TFixture _fixture;
    protected abstract string ExpectedClassName { get; }
    protected abstract string UnexpectedClassName { get; }
    protected const string LEGACY_CLASS_NAME = "System.Net.Http.HttpClient";
    protected const string CLASS_NAME = "System.Net.Http.SocketsHttpHandler";
    protected const string METHOD_NAME = "SendAsync";

    private const string PostUri = "http://www.google.com";
    private const string PutUri = "http://www.yahoo.com";

    protected HttpClientBodyInstrumentationTestsBase(TFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.AddActions(
            setupConfiguration: () =>
            {
                new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                    .ForceTransactionTraces()
                    .SetLogLevel("finest");
            },
            exerciseApplication: () =>
            {
                // The POST and PUT commands should each finish their transform.
                _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 2);
            }
        );

        _fixture.AddCommand($"HttpClientDriver Post {PostUri}");
        _fixture.AddCommand($"HttpClientDriver Put {PutUri}");

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var expectedMetrics = new List<Assertions.ExpectedMetric>
        {
            // Exactly one external segment per body request - a duplicate from the HttpWebRequest
            // body wrappers would push these counts to 4.
            new Assertions.ExpectedMetric { metricName = @"External/all", CallCountAllHarvests = 2 },
            new Assertions.ExpectedMetric { metricName = @"External/allOther", CallCountAllHarvests = 2 },

            new Assertions.ExpectedMetric { metricName = @"External/www.google.com/all", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/POST", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/www.google.com/Stream/POST", metricScope = @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Post", callCount = 1 },

            new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/all", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/PUT", callCount = 1 },
            new Assertions.ExpectedMetric { metricName = @"External/www.yahoo.com/Stream/PUT", metricScope = @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Put", callCount = 1 },
        };

        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var postTransactionSample = _fixture.AgentLog.GetTransactionSamples()
            .FirstOrDefault(sample => sample.Path == @"OtherTransaction/Custom/MultiFunctionApplicationHelpers.NetStandardLibraries.Internal.HttpClientDriver/Post");

        Assert.NotNull(postTransactionSample);

        // Exactly one http (external) span per body request - a duplicate would make this 4.
        var externalSpanEvents = _fixture.AgentLog.GetSpanEvents()
            .Where(e => e.IntrinsicAttributes.TryGetValue("category", out var value) && value.Equals("http"))
            .ToList();
        Assert.Equal(2, externalSpanEvents.Count);

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(expectedMetrics, metrics),
            () => Assertions.TransactionTraceSegmentsExist(new List<string> { @"External/www.google.com/Stream/POST" }, postTransactionSample),
            // The external segment is owned by the HttpClient instrumentation, not a bare
            // HttpWebRequest wrapper.
            () => Assertions.TransactionTraceSegmentExists(ExpectedClassName, METHOD_NAME, postTransactionSample),
            () => Assertions.TransactionTraceSegmentDoesNotExist(UnexpectedClassName, METHOD_NAME, postTransactionSample),
            () => Assert.All(externalSpanEvents, e => Assert.True(e.IntrinsicAttributes.TryGetValue("component", out var value) && value.ToString().StartsWith("System.Net.Http.")))
        );

        var agentWrapperErrorRegex = AgentLogBase.ErrorLogLinePrefixRegex + @"An exception occurred in a wrapper: (.*)";
        var wrapperError = _fixture.AgentLog.TryGetLogLine(agentWrapperErrorRegex);

        Assert.Null(wrapperError);
    }
}

public class HttpClientBodyInstrumentationTests_NetCoreOldest : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
{
    protected override string ExpectedClassName { get { return CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return LEGACY_CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_NetCoreOldest(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientBodyInstrumentationTests_NetCoreLatest : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
{
    protected override string ExpectedClassName { get { return CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return LEGACY_CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_NetCoreLatest(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientBodyInstrumentationTests_FW462 : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW462>
{
    protected override string ExpectedClassName { get { return LEGACY_CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_FW462(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientBodyInstrumentationTests_FW471 : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW471>
{
    protected override string ExpectedClassName { get { return LEGACY_CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_FW471(ConsoleDynamicMethodFixtureFW471 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientBodyInstrumentationTests_FW48 : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureFW48>
{
    protected override string ExpectedClassName { get { return LEGACY_CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_FW48(ConsoleDynamicMethodFixtureFW48 fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}

public class HttpClientBodyInstrumentationTests_FWLatest : HttpClientBodyInstrumentationTestsBase<ConsoleDynamicMethodFixtureFWLatest>
{
    protected override string ExpectedClassName { get { return LEGACY_CLASS_NAME; } }
    protected override string UnexpectedClassName { get { return CLASS_NAME; } }

    public HttpClientBodyInstrumentationTests_FWLatest(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
        : base(fixture, output)
    {
    }
}
