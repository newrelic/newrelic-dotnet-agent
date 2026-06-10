// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.OpenTelemetry;

// ---------------------------------------------------------------------------
// Scenario 1: Happy path — mock always returns 200
// Validates: export/success is recorded; retry and failure are absent.
// .NET Core only — CustomRetryHandler (which emits these metrics) is
// compiled only on NETSTANDARD2_0_OR_GREATER. Framework targets are deferred.
// ---------------------------------------------------------------------------
public abstract class OtlpExportSuccessMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : OtlpMetricsWithCollectorFixtureBase
{
    private const string ExportSuccess = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/success";
    private const string ExportRetry   = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/retry";
    private const string ExportFailure = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/failure";

    protected readonly TFixture _fixture;

    protected OtlpExportSuccessMetricsTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = outputHelper;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.SetOpenTelemetryMetricsExportInterval(5000);
                configModifier.SetOpenTelemetryMetricsExportTimeout(4000);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.OtlpMetricsExportedLogLineRegex, TimeSpan.FromMinutes(1));

                // Wait up to 30s for the first harvest to report export/success
                var deadline = DateTime.Now.AddSeconds(30);
                while (DateTime.Now < deadline &&
                       !_fixture.AgentLog.GetMetrics().Any(m => m.MetricSpec.Name == ExportSuccess))
                {
                    Thread.Sleep(500);
                }
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ExportSuccess_IsRecorded()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var successMetrics = metrics.Where(m => m.MetricSpec.Name == ExportSuccess).ToList();
        Assert.True(successMetrics.Any(), $"Expected metric '{ExportSuccess}' not found in agent metrics.");
        var totalCount = successMetrics.Sum(m => (decimal)m.Values.CallCount);
        Assert.True(totalCount >= 1, $"Expected '{ExportSuccess}' callCount >= 1, got {totalCount}");

        Assertions.MetricsDoNotExist(new[]
        {
            new Assertions.ExpectedMetric { metricName = ExportRetry },
            new Assertions.ExpectedMetric { metricName = ExportFailure }
        }, metrics);
    }
}

public class OtlpExportSuccessMetricsTestsCoreLatest : OtlpExportSuccessMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest>
{
    public OtlpExportSuccessMetricsTestsCoreLatest(OtlpMetricsWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

public class OtlpExportSuccessMetricsTestsCoreNet8 : OtlpExportSuccessMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8>
{
    public OtlpExportSuccessMetricsTestsCoreNet8(OtlpMetricsWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

// ---------------------------------------------------------------------------
// Scenario 2: Retry-then-succeed — mock fails first 2 requests with 503,
// then returns 200. CustomRetryHandler retries up to MaxRetries=3 total
// attempts, so attempt 1 fails (retry emitted), attempt 2 fails (retry
// emitted), attempt 3 succeeds (success emitted).
// Validates: export/retry >= 2, export/success >= 1, export/failure absent.
// ---------------------------------------------------------------------------
public abstract class OtlpExportRetryMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : OtlpMetricsWithCollectorFixtureBase
{
    private const string ExportSuccess = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/success";
    private const string ExportRetry   = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/retry";
    private const string ExportFailure = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/failure";

    protected readonly TFixture _fixture;

    protected OtlpExportRetryMetricsTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = outputHelper;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.SetOpenTelemetryMetricsExportInterval(5000);
                configModifier.SetOpenTelemetryMetricsExportTimeout(4000);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));

                // Configure the mock to fail the first 2 OTLP requests with 503
                _fixture.ConfigureOtlpFailures(503, 2);

                // Wait up to 30s for export/success (signals the retry cycle completed)
                var deadline = DateTime.Now.AddSeconds(30);
                while (DateTime.Now < deadline &&
                       !_fixture.AgentLog.GetMetrics().Any(m => m.MetricSpec.Name == ExportSuccess))
                {
                    Thread.Sleep(500);
                }
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ExportRetry_AndSuccess_AreRecorded_ExportFailure_IsAbsent()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var retryMetrics = metrics.Where(m => m.MetricSpec.Name == ExportRetry).ToList();
        Assert.True(retryMetrics.Any(), $"Expected metric '{ExportRetry}' not found.");
        var totalRetry = retryMetrics.Sum(m => (decimal)m.Values.CallCount);
        Assert.True(totalRetry >= 2, $"Expected '{ExportRetry}' callCount >= 2, got {totalRetry}");

        var successMetrics = metrics.Where(m => m.MetricSpec.Name == ExportSuccess).ToList();
        Assert.True(successMetrics.Any(), $"Expected metric '{ExportSuccess}' not found.");
        var totalSuccess = successMetrics.Sum(m => (decimal)m.Values.CallCount);
        Assert.True(totalSuccess >= 1, $"Expected '{ExportSuccess}' callCount >= 1, got {totalSuccess}");

        Assertions.MetricsDoNotExist(new[]
        {
            new Assertions.ExpectedMetric { metricName = ExportFailure }
        }, metrics);
    }
}

public class OtlpExportRetryMetricsTestsCoreLatest : OtlpExportRetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest>
{
    public OtlpExportRetryMetricsTestsCoreLatest(OtlpMetricsWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

public class OtlpExportRetryMetricsTestsCoreNet8 : OtlpExportRetryMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8>
{
    public OtlpExportRetryMetricsTestsCoreNet8(OtlpMetricsWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

// ---------------------------------------------------------------------------
// Scenario 3: Full exhaustion — mock always returns 503.
// CustomRetryHandler exhausts all 3 attempts per export cycle, emitting
// ExportRetry twice (before attempt 2 and attempt 3) and ExportFailure once
// (after attempt 3 fails).
// Validates: export/failure >= 1, export/retry >= 2, export/success absent.
// ---------------------------------------------------------------------------
public abstract class OtlpExportFailureMetricsTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : OtlpMetricsWithCollectorFixtureBase
{
    private const string ExportSuccess = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/success";
    private const string ExportRetry   = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/retry";
    private const string ExportFailure = "Supportability/Metrics/DotNet/OpenTelemetryBridge/export/failure";

    protected readonly TFixture _fixture;

    protected OtlpExportFailureMetricsTestsBase(TFixture fixture, ITestOutputHelper outputHelper) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = outputHelper;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.SetOpenTelemetryMetricsExportInterval(5000);
                configModifier.SetOpenTelemetryMetricsExportTimeout(4000);
            },
            exerciseApplication: () =>
            {
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));

                // Configure the mock to fail all OTLP requests indefinitely
                _fixture.ConfigureOtlpFailures(503, -1);

                // Wait up to 30s for export/failure to appear in harvested metrics
                var deadline = DateTime.Now.AddSeconds(30);
                while (DateTime.Now < deadline &&
                       !_fixture.AgentLog.GetMetrics().Any(m => m.MetricSpec.Name == ExportFailure))
                {
                    Thread.Sleep(500);
                }
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ExportFailure_AndRetry_AreRecorded_ExportSuccess_IsAbsent()
    {
        var metrics = _fixture.AgentLog.GetMetrics().ToList();

        var failureMetrics = metrics.Where(m => m.MetricSpec.Name == ExportFailure).ToList();
        Assert.True(failureMetrics.Any(), $"Expected metric '{ExportFailure}' not found.");
        var totalFailure = failureMetrics.Sum(m => (decimal)m.Values.CallCount);
        Assert.True(totalFailure >= 1, $"Expected '{ExportFailure}' callCount >= 1, got {totalFailure}");

        var retryMetrics = metrics.Where(m => m.MetricSpec.Name == ExportRetry).ToList();
        Assert.True(retryMetrics.Any(), $"Expected metric '{ExportRetry}' not found.");
        var totalRetry = retryMetrics.Sum(m => (decimal)m.Values.CallCount);
        Assert.True(totalRetry >= 2, $"Expected '{ExportRetry}' callCount >= 2, got {totalRetry}");

        Assertions.MetricsDoNotExist(new[]
        {
            new Assertions.ExpectedMetric { metricName = ExportSuccess }
        }, metrics);
    }
}

public class OtlpExportFailureMetricsTestsCoreLatest : OtlpExportFailureMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreLatest>
{
    public OtlpExportFailureMetricsTestsCoreLatest(OtlpMetricsWithCollectorFixtureCoreLatest fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}

public class OtlpExportFailureMetricsTestsCoreNet8 : OtlpExportFailureMetricsTestsBase<OtlpMetricsWithCollectorFixtureCoreNet8>
{
    public OtlpExportFailureMetricsTestsCoreNet8(OtlpMetricsWithCollectorFixtureCoreNet8 fixture, ITestOutputHelper outputHelper) : base(fixture, outputHelper) { }
}
