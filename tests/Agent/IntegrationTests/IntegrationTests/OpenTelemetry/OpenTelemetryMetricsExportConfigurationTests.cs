// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry;

public abstract class OpenTelemetryMetricsExportConfigurationTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : OtlpMetricsWithCollectorFixtureBase
{
    protected readonly TFixture _fixture;

    protected OpenTelemetryMetricsExportConfigurationTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = outputHelper;
    }

    protected void SetupInvalidConfiguration(int intervalMs, int timeoutMs)
    {
        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
                configModifier.SetOpenTelemetryMetricsExportInterval(intervalMs);
                configModifier.SetOpenTelemetryMetricsExportTimeout(timeoutMs);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    protected void SetupValidConfiguration(int intervalMs, int timeoutMs)
    {
        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
                configModifier.SetOpenTelemetryMetricsExportInterval(intervalMs);
                configModifier.SetOpenTelemetryMetricsExportTimeout(timeoutMs);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }
}

public class OpenTelemetryMetricsExportConfigurationInvalidIntervalLessThanTimeoutTests : OpenTelemetryMetricsExportConfigurationTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest>
{
    public OpenTelemetryMetricsExportConfigurationInvalidIntervalLessThanTimeoutTests(OtlpMetricsWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) 
        : base(fixture, outputHelper)
    {
        // Setup with interval < timeout (invalid)
        SetupInvalidConfiguration(intervalMs: 5000, timeoutMs: 10000);
    }

    [Fact]
    public void InvalidConfig_IntervalLessThanTimeout_LogsWarning()
    {
        var logLines = _fixture.AgentLog.GetFileLines().ToList();

        var warningLogLine = logLines.FirstOrDefault(line =>
            line.Contains("WARN") &&
            line.Contains("OpenTelemetry metrics export interval") &&
            line.Contains("is less than export timeout") &&
            line.Contains("Reverting to defaults"));

        NrAssert.Multiple(
            () => Assert.NotNull(warningLogLine),
            () => Assert.Contains("5000 ms", warningLogLine),
            () => Assert.Contains("10000 ms", warningLogLine),
            () => Assert.Contains("interval=60000 ms", warningLogLine),
            () => Assert.Contains("timeout=10000 ms", warningLogLine)
        );
    }
}

public class OpenTelemetryMetricsExportConfigurationValidIntervalEqualsTimeoutTests : OpenTelemetryMetricsExportConfigurationTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8>
{
    public OpenTelemetryMetricsExportConfigurationValidIntervalEqualsTimeoutTests(OtlpMetricsWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) 
        : base(fixture, outputHelper)
    {
        // Setup with interval == timeout (currently valid per implementation)
        SetupValidConfiguration(intervalMs: 15000, timeoutMs: 15000);
    }

    [Fact]
    public void ValidConfig_IntervalEqualsTimeout_NoWarning()
    {
        var logLines = _fixture.AgentLog.GetFileLines().ToList();

        var warningLogLine = logLines.FirstOrDefault(line =>
            line.Contains("WARN") &&
            line.Contains("OpenTelemetry metrics export interval") &&
            line.Contains("is less than export timeout"));

        Assert.Null(warningLogLine);
    }
}

public class OpenTelemetryMetricsExportConfigurationValidTests : OpenTelemetryMetricsExportConfigurationTestsBase<OtlpMetricsWithCollectorFixtureFW472>
{
    public OpenTelemetryMetricsExportConfigurationValidTests(OtlpMetricsWithCollectorFixtureFW472 fixture, ITestOutputHelper outputHelper) 
        : base(fixture, outputHelper)
    {
        // Setup with interval > timeout (valid)
        SetupValidConfiguration(intervalMs: 70000, timeoutMs: 20000);
    }

    [Fact]
    public void ValidConfig_IntervalGreaterThanTimeout_NoWarning()
    {
        var logLines = _fixture.AgentLog.GetFileLines().ToList();

        var warningLogLine = logLines.FirstOrDefault(line =>
            line.Contains("WARN") &&
            line.Contains("OpenTelemetry metrics export interval") &&
            line.Contains("is less than export timeout"));

        Assert.Null(warningLogLine);
    }
}
