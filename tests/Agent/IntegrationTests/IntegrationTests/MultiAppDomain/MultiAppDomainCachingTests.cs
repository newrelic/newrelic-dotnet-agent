// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.MultiAppDomain;

/// <summary>
/// Two ASP.NET Framework applications in one Hosted Web Core process: one application pool, one
/// process, two AppDomains, two agents. Verifies that neither agent's state leaks into the other
/// under both AppDomain-caching arms.
///
/// This matters on this branch specifically. The AppDomain-caching fast path here reads and writes
/// static fields on a profiler-injected type (__NRInitializer__), and on .NET Framework mscorlib is
/// always loaded domain-neutral, so there is one field definition and one shared JIT'd helper body
/// but a separate field VALUE per AppDomain. The design is correct by construction; nothing in this
/// repository exercised it, because the storage that ships on main is AppDomain.GetData/SetData,
/// which is per-domain by API contract rather than by runtime indirection.
/// </summary>
public abstract class MultiAppDomainCachingTestsBase : NewRelicIntegrationTest<MultiAppDomainWebApplicationFixture>
{
    // Generous relative to the sibling AppDomainCachingTestsBase, which allows 2 minutes total for a
    // console fixture. Two agents connect behind one Hosted Web Core cold start here.
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan MetricHarvestTimeout = TimeSpan.FromMinutes(2);

    protected readonly MultiAppDomainWebApplicationFixture _fixture;

    private readonly bool _appDomainCachingDisabled;

    private readonly string _expectedCallingStrategy;

    protected MultiAppDomainCachingTestsBase(
        MultiAppDomainWebApplicationFixture fixture,
        ITestOutputHelper output,
        bool appDomainCachingDisabled,
        string expectedCallingStrategy) : base(fixture)
    {
        _fixture = fixture;
        _appDomainCachingDisabled = appDomainCachingDisabled;
        _expectedCallingStrategy = expectedCallingStrategy;
        _fixture.TestLogger = output;

        // Arm A leaves the variable completely UNSET, which is the state every real customer runs and
        // a different parse path from an explicitly present "false". This diverges deliberately from
        // AppDomainCachingTestsBase, which sets the variable in both of its arms.
        if (_appDomainCachingDisabled)
        {
            _fixture.SetAdditionalEnvironmentVariable("NEW_RELIC_DISABLE_APPDOMAIN_CACHING", "true");
        }

        _fixture.Actions(
            setupConfiguration: () =>
            {
                // One newrelic.config serves both AppDomains, so one modifier covers both.
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.ConfigureFasterMetricsHarvestCycle(10);   // assertions 1 and 5
                configModifier.ForceTransactionTraces();                 // assertion 2
                configModifier.SetLogLevel("debug");
            },
            exerciseApplication: () =>
            {
                // Four steps, each killing a distinct failure mode. Do not reorder or drop any.

                // 1. Cold start cannot be allowed to eat a counted request.
                _fixture.WarmUpRootApp();
                _fixture.WarmUpSecondApp();

                // 2. Both agents connected before any counted request removes the init race.
                _fixture.RootAppAgentLog.WaitForConnect(ConnectTimeout);
                _fixture.SecondAppAgentLog.WaitForConnect(ConnectTimeout);

                // 3. Asymmetric load, so a leak cannot hide behind equal counts.
                for (var i = 0; i < MultiAppDomainWebApplicationFixture.RootAppRequestCount; i++)
                {
                    _fixture.GetRootAppCustomAttributes();
                }

                for (var i = 0; i < MultiAppDomainWebApplicationFixture.SecondAppRequestCount; i++)
                {
                    _fixture.GetSecondAppCustomAttributes();
                }

                // 4. Gate on the harvested aggregate rather than on a sleep, so harvest lag cannot
                //    produce a short count that looks like a leak.
                _fixture.RootAppAgentLog.WaitForMetricAggregateCallCount(
                    MultiAppDomainWebApplicationFixture.ExpectedTransactionMetricName,
                    MultiAppDomainWebApplicationFixture.RootAppRequestCount,
                    MetricHarvestTimeout);

                _fixture.SecondAppAgentLog.WaitForMetricAggregateCallCount(
                    MultiAppDomainWebApplicationFixture.ExpectedTransactionMetricName,
                    MultiAppDomainWebApplicationFixture.SecondAppRequestCount,
                    MetricHarvestTimeout);
            });

        _fixture.Initialize();
    }

    [Fact]
    public void EachAppReportsOnlyItsOwnTransactions()
    {
        var rootCallCount = GetUnscopedCallCount(_fixture.RootAppAgentLog, MultiAppDomainWebApplicationFixture.ExpectedTransactionMetricName);
        var secondCallCount = GetUnscopedCallCount(_fixture.SecondAppAgentLog, MultiAppDomainWebApplicationFixture.ExpectedTransactionMetricName);

        // EXACT, never >=. A >= comparison would pass while a leak inflated the other app's count.
        Assert.Multiple(
            () => Assert.Equal((ulong)MultiAppDomainWebApplicationFixture.RootAppRequestCount, rootCallCount),
            () => Assert.Equal((ulong)MultiAppDomainWebApplicationFixture.SecondAppRequestCount, secondCallCount));
    }

    private static ulong GetUnscopedCallCount(AgentLogFile agentLog, string metricName)
    {
        ulong total = 0;

        var matches = agentLog.GetMetrics()
            .Where(metric => metric.MetricSpec.Name == metricName && string.IsNullOrEmpty(metric.MetricSpec.Scope));

        foreach (var metric in matches)
        {
            total += metric.Values.CallCount;
        }

        return total;
    }

    [Fact]
    public void EachAppReportsOnlyItsOwnCustomAttributes()
    {
        AssertEveryMarkedTraceCarries(
            _fixture.RootAppAgentLog,
            MultiAppDomainWebApplicationFixture.RootAppMarkerValue,
            MultiAppDomainWebApplicationFixture.SecondAppMarkerValue);

        AssertEveryMarkedTraceCarries(
            _fixture.SecondAppAgentLog,
            MultiAppDomainWebApplicationFixture.SecondAppMarkerValue,
            MultiAppDomainWebApplicationFixture.RootAppMarkerValue);
    }

    private static void AssertEveryMarkedTraceCarries(AgentLogFile agentLog, string expectedValue, string forbiddenValue)
    {
        var markedSamples = agentLog.GetTransactionSamples()
            .Where(sample => sample.TraceData.Attributes
                .GetByType(TransactionTraceAttributeType.User)
                .ContainsKey(MultiAppDomainWebApplicationFixture.AppMarkerAttributeName))
            .ToList();

        // Traces yield roughly one sample per application per harvest rather than one per request,
        // which is enough for a presence/absence assertion but means the count is not asserted here.
        Assert.NotEmpty(markedSamples);

        foreach (var sample in markedSamples)
        {
            Assertions.TransactionTraceHasAttributes(
                new Dictionary<string, string> { { MultiAppDomainWebApplicationFixture.AppMarkerAttributeName, expectedValue } },
                TransactionTraceAttributeType.User,
                sample);

            var userAttributes = sample.TraceData.Attributes.GetByType(TransactionTraceAttributeType.User);
            Assert.NotEqual(forbiddenValue, userAttributes[MultiAppDomainWebApplicationFixture.AppMarkerAttributeName]?.ToString());
        }
    }

    [Fact]
    public void TwoIndependentAgentsStartOnePerAppDomain()
    {
        // AgentManager.cs:238 logs: "The New Relic .NET Agent v<version> started (pid <n>) on app
        // domain '<AppDomainAppVirtualPath ?? AppDomainName>'". Asserting the virtual path also
        // exercises HttpRuntime.AppDomainAppVirtualPath reaching the agent, without the test
        // depending on it for naming.
        var rootStartLines = _fixture.RootAppAgentLog.TryGetLogLines(AgentStartedOnAppDomainRegex("/")).ToList();
        var secondStartLines = _fixture.SecondAppAgentLog.TryGetLogLines(AgentStartedOnAppDomainRegex("/app2")).ToList();

        // Single, not NotEmpty: exactly one agent per AppDomain is part of what is being asserted.
        Assert.Multiple(
            () => Assert.Single(rootStartLines),
            () => Assert.Single(secondStartLines));
    }

    private static string AgentStartedOnAppDomainRegex(string appDomainVirtualPath)
    {
        return AgentLogBase.InfoLogLinePrefixRegex
            + @"The New Relic .NET Agent v.* started \(pid \d+\) on app domain '"
            + Regex.Escape(appDomainVirtualPath)
            + @"'";
    }

    [Fact]
    public void EachAppConnectsUnderItsOwnApplicationName()
    {
        var rootAppNames = _fixture.RootAppAgentLog.GetConnectData().AppNames.ToList();
        var secondAppNames = _fixture.SecondAppAgentLog.GetConnectData().AppNames.ToList();

        Assert.Multiple(
            () => Assert.Contains(MultiAppDomainWebApplicationFixture.RootAppReportedName, rootAppNames),
            () => Assert.DoesNotContain(MultiAppDomainWebApplicationFixture.SecondAppReportedName, rootAppNames),
            () => Assert.Contains(MultiAppDomainWebApplicationFixture.SecondAppReportedName, secondAppNames),
            () => Assert.DoesNotContain(MultiAppDomainWebApplicationFixture.RootAppReportedName, secondAppNames));
    }

    [Fact]
    public void AppDomainCachingSupportabilityMetricAgreesWithArmInBothLogs()
    {
        const string metricName = "Supportability/DotNET/AppDomainCaching/Disabled";

        var rootMetrics = _fixture.RootAppAgentLog.GetMetrics().ToList();
        var secondMetrics = _fixture.SecondAppAgentLog.GetMetrics().ToList();

        if (_appDomainCachingDisabled)
        {
            Assert.Multiple(
                () => Assert.Contains(rootMetrics, metric => metric.MetricSpec.Name == metricName),
                () => Assert.Contains(secondMetrics, metric => metric.MetricSpec.Name == metricName));
        }
        else
        {
            Assert.Multiple(
                () => Assert.DoesNotContain(rootMetrics, metric => metric.MetricSpec.Name == metricName),
                () => Assert.DoesNotContain(secondMetrics, metric => metric.MetricSpec.Name == metricName));
        }
    }

    [Fact]
    public void BothAgentsStartAndStopCleanly()
    {
        // Verified strings, not guesses:
        //   AgentManager.cs:68  Log.Error(exception, "There was an error initializing the agent")
        //   AgentManager.cs:427 Log.Info(e, "Unexpected exception during agent shutdown")  <- INFO,
        //                       not ERROR, so it must be matched at the INFO prefix.
        // The two exception type names are scenario-specific cross-domain failures that would appear
        // in a logged stack trace; they are matched level-agnostically.
        var forbiddenPatterns = new[]
        {
            AgentLogBase.ErrorLogLinePrefixRegex + @"There was an error initializing the agent",
            AgentLogBase.InfoLogLinePrefixRegex + @"Unexpected exception during agent shutdown",
            @"AppDomainUnloadedException",
            @"RemotingException",
        };

        var logs = new Dictionary<string, AgentLogFile>
        {
            { "/", _fixture.RootAppAgentLog },
            { "/app2", _fixture.SecondAppAgentLog },
        };

        foreach (var log in logs)
        {
            foreach (var pattern in forbiddenPatterns)
            {
                Assert.Empty(log.Value.TryGetLogLines(pattern));
            }

            // Positive half: each agent reached its own clean shutdown line
            // (AgentManager.cs:436), which is what proves two teardowns both completed.
            Assert.Single(log.Value.TryGetLogLines(AgentShutdownOnAppDomainRegex(log.Key)).ToList());
        }
    }

    private static string AgentShutdownOnAppDomainRegex(string appDomainVirtualPath)
    {
        return AgentLogBase.InfoLogLinePrefixRegex
            + @"The New Relic .NET Agent v.* has shutdown \(pid \d+\) on app domain '"
            + Regex.Escape(appDomainVirtualPath)
            + @"'";
    }

    [Fact]
    public void ProfilerUsesTheCallingStrategyThisArmSelected()
    {
        // LOAD-BEARING. This is the only assertion that proves the two arms take different paths.
        // Every other assertion in this class would pass identically under either strategy, so if the
        // environment variable stopped reaching the child process both arms would go green while the
        // disabled path went uncovered.
        Assert.Contains(
            $"Calls to the managed agent will use the calling strategy - {_expectedCallingStrategy}",
            _fixture.ProfilerLog.GetFullLogAsString());
    }
}

[Trait("Runtime", "Framework")]
public class MultiAppDomainCachingEnabledTests : MultiAppDomainCachingTestsBase
{
    public MultiAppDomainCachingEnabledTests(MultiAppDomainWebApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, false, "AppDomain Fallback Cache")
    {
    }
}

[Trait("Runtime", "Framework")]
public class MultiAppDomainCachingDisabledTests : MultiAppDomainCachingTestsBase
{
    public MultiAppDomainCachingDisabledTests(MultiAppDomainWebApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, true, "Reflection")
    {
    }
}
