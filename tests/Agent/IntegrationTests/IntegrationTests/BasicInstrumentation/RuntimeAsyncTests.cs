// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.BasicInstrumentation;

/// <summary>
/// Covers instrumentation of .NET 11 runtime-async methods. The application is built for
/// net10.0 with runtime-async enabled as a preview feature, so this needs no .NET 11 SDK.
///
/// The assertion that matters is the scoped count of the nested InnerAsync segment. InnerAsync
/// is called twice per OuterAsync invocation, both times after a suspension point, so its
/// continuations resume on whatever thread the pool supplies. When a runtime-async transaction
/// was left stranded in its creating thread's primary storage, roughly a quarter of those
/// nested segments were silently dropped -- no error, no unfinished segment, just a lower
/// count. Only an exact count catches that.
///
/// Requires a profiler that can instrument runtime-async methods. Against an older profiler
/// these methods are rewritten incorrectly and the application dies with
/// InvalidProgramException rather than failing an assertion -- if that is what you are seeing,
/// check the profiler build rather than this test.
/// </summary>
public class RuntimeAsyncTests : NewRelicIntegrationTest<RuntimeAsyncTestsFixture>
{
    private readonly RuntimeAsyncTestsFixture _fixture;

    // Must match RuntimeAsyncUseCases.Iterations in the test application.
    private const int Iterations = 25;

    // OuterAsync calls InnerAsync twice per invocation.
    private const int ExpectedNestedSegmentCount = Iterations * 2;

    // Attribute-instrumented methods name their transaction with the Custom/ category but
    // their segments with the DotNet/ prefix.
    private const string OuterTransactionName = @"OtherTransaction/Custom/RuntimeAsyncApplication.RuntimeAsyncUseCases/OuterAsync";
    private const string OuterSegmentName = @"DotNet/RuntimeAsyncApplication.RuntimeAsyncUseCases/OuterAsync";
    private const string InnerSegmentName = @"DotNet/RuntimeAsyncApplication.RuntimeAsyncUseCases/InnerAsync";

    public RuntimeAsyncTests(RuntimeAsyncTestsFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                // Runtime-async IL is rejected at type load on .NET 10 without this. The
                // failure is a TypeLoadException before any assertion runs, so a missing
                // variable fails the test loudly rather than quietly weakening it.
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("DOTNET_RuntimeAsync", "1");

                // Matches NetCoreAsyncTests, the closest sibling: the event listener samplers
                // add unrelated background activity to a short-lived console app.
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.DisableEventListenerSamplers();
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        Assert.NotNull(metrics);

        NrAssert.Multiple
        (
            () => Assertions.MetricsExist(_expectedMetrics, metrics)
        );
    }

    private readonly List<Assertions.ExpectedMetric> _expectedMetrics = new List<Assertions.ExpectedMetric>
    {
        // Exactly one transaction per iteration. InnerAsync carries [Transaction] too, so if
        // nesting broke and it started its own transaction instead of recording a segment,
        // this count would come in at three times the expected value.
        new Assertions.ExpectedMetric { metricName = OuterTransactionName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = @"OtherTransaction/all", CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = @"OtherTransactionTotalTime", CallCountAllHarvests = Iterations },

        new Assertions.ExpectedMetric { metricName = OuterSegmentName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, CallCountAllHarvests = ExpectedNestedSegmentCount },

        // The load-bearing assertions: both segments recorded *within* OuterAsync's
        // transaction, the nested one on every single invocation.
        new Assertions.ExpectedMetric { metricName = OuterSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = Iterations },
        new Assertions.ExpectedMetric { metricName = InnerSegmentName, metricScope = OuterTransactionName, CallCountAllHarvests = ExpectedNestedSegmentCount },
    };
}
