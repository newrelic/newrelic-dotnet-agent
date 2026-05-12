// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

public abstract class OtlpMetricsWithCollectorFixtureBase : MockNewRelicFixture
{
    protected OtlpMetricsWithCollectorFixtureBase(string targetFramework, bool isCoreApp, string applicationDirectoryName = "OTelMetricsApplication", string executableName = "OTelMetricsApplication.exe") :
        base(new RemoteService(
            applicationDirectoryName,
            executableName,
            targetFramework,
            ApplicationType.Bounded,
            true,
            isCoreApp,
            true))
    {
        AddActions(setupConfiguration: () =>
        {
            NewRelicConfigModifier configModifier = new NewRelicConfigModifier(DestinationNewRelicConfigFilePath);

            configModifier.EnableOpenTelemetry(true);
            configModifier.EnableOpenTelemetryMetrics(true);

            configModifier.IncludeOpenTelemetryMeters("OtelMetricsTest.App");

            // disable event pipe integration due to a known conflict with the OTEL SDK logger
            // TODO: Remove this line when the conflict is resolved
            configModifier.EnableEventListenerSamplers(false);
        });
    }

    public IEnumerable<MetricsSummaryDto> GetCollectedOTLPMetrics(int count = 1)
    {
        var address = $"https://localhost:{MockNewRelicApplication.Port}/v1/metrics/collected?count={count}";

        TestLogger?.WriteLine($"[MockNewRelicFixture] Get collected OTLP Metrics via: {address}");

        return GetJson<List<MetricsSummaryDto>>(address) ?? new List<MetricsSummaryDto>();
    }

    public int GetCollectedOTLPMetricsCount()
    {
        var address = $"https://localhost:{MockNewRelicApplication.Port}/v1/metrics/count";
        TestLogger?.WriteLine($"[MockNewRelicFixture] Get collected OTLP Metrics count via: {address}");
        return Convert.ToInt32(GetString(address));
    }

    public void ClearCollectedOTLPMetrics()
    {
        var address = $"https://localhost:{MockNewRelicApplication.Port}/v1/metrics/clear";
        TestLogger?.WriteLine($"[MockNewRelicFixture] Clear collected OTLP Metrics via: {address}");
        GetJson<string>(address);
    }
}

public class OtlpMetricsWithCollectorFixtureCoreLatest : OtlpMetricsWithCollectorFixtureBase
{
    public OtlpMetricsWithCollectorFixtureCoreLatest() : base(Tfm.NetLatest, true)
    {
    }
}

public class OtlpMetricsWithCollectorFixtureCoreOldest : OtlpMetricsWithCollectorFixtureBase
{
    public OtlpMetricsWithCollectorFixtureCoreOldest() : base(Tfm.NetOldest, true)
    {
    }
}

public class OtlpMetricsWithCollectorFixtureFWLatest : OtlpMetricsWithCollectorFixtureFW481
{
}

public class OtlpMetricsWithCollectorFixtureFW481 : OtlpMetricsWithCollectorFixtureBase
{
    public OtlpMetricsWithCollectorFixtureFW481() : base("net481", false)
    {
    }
}

public class OtlpMetricsWithCollectorFixtureFW472 : OtlpMetricsWithCollectorFixtureBase
{
    public OtlpMetricsWithCollectorFixtureFW472() : base("net472", false)
    {
    }
}

public class OtlpMetricsWithCollectorFixtureFW462 : OtlpMetricsWithCollectorFixtureBase
{
    public OtlpMetricsWithCollectorFixtureFW462() : base("net462", false)
    {
    }
}