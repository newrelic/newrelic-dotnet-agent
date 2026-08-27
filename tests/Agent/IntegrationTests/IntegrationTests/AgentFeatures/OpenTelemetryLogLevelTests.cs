// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

public class OpenTelemetryLogLevelTests : NewRelicIntegrationTest<ConsoleDynamicMethodFixtureCoreLatest>
{
    private readonly ConsoleDynamicMethodFixtureCoreLatest _fixture;

    public OpenTelemetryLogLevelTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                _fixture.RemoteApplication.NewRelicConfig.EnableOpenTelemetry(true);
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("OTEL_LOG_LEVEL", "debug");

                _fixture.AddCommand("HttpClientDriver Get");
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void OtelLogLevelDebugTakesEffect()
    {
        // shipped default newrelic.config is <log level="info" />, so debug lines only appear
        // if OTEL_LOG_LEVEL=debug actually took effect via the OTel bridge mapping
        var match = _fixture.AgentLog.WaitForLogLine(@"DEBUG: \[pid: ", TimeSpan.FromSeconds(30));
        Assert.NotNull(match);
    }
}
