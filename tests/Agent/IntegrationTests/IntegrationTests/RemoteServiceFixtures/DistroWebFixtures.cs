// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;

// OTel distro variants of the hosted web core (HWC) BasicMvcApplication fixtures: HWC (launched as a HostedWebCore.exe
// copy named w3wp.exe, see RemoteWebApplication) runs under the OpenTelemetry .NET auto-instrumentation native profiler
// + the New Relic distro plugin instead of the New Relic profiler (RemoteApplication.UseOtelDistro). They subclass the
// agent-mode fixtures so the same test bases and exercise helpers apply. No New Relic profiler is attached, so no
// profiler log is expected. Requires NR_DEV_OTEL_HOME.

public class BasicMvcApplicationTestFixtureDistro : BasicMvcApplicationTestFixture
{
    public BasicMvcApplicationTestFixtureDistro()
    {
        RemoteApplication.UseOtelDistro = true;
        ProfilerLogExpected = false;
    }
}

public class DTBasicMVCApplicationFixtureDistro : DTBasicMVCApplicationFixture
{
    public DTBasicMVCApplicationFixtureDistro()
    {
        RemoteApplication.UseOtelDistro = true;
        ProfilerLogExpected = false;
        // Finest log level is kept for log-based evidence only (the finest NR log shows the HttpClient caller's DT
        // payload creation); the DT assertions themselves are on metrics. The env var wins over the config's log level,
        // and is applied after the harness strips ambient NEW_RELIC_* vars.
        RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_LOG_LEVEL", "finest");
    }
}
