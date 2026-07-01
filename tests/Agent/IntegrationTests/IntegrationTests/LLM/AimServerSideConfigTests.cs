// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.LLM;

// Local config: AIM off, streaming on, record_content on.
// Server agent_config: AIM on, streaming off, record_content off.
// Expect resolved: enabled=true (SSC enables master), streaming=false, record_content=false (SSC overrides local).
public class AimServerSideConfigOverridesLocal : NewRelicIntegrationTest<MvcWithCollectorFixture>
{
    private readonly MvcWithCollectorFixture _fixture;

    public AimServerSideConfigOverridesLocal(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                configModifier.SetLogLevel("finest");
                configModifier.EnableAiMonitoring(false);
                configModifier.EnableAiMonitoringStreaming(true);
                configModifier.EnableAiMonitoringRecordContent(true);
            },
            exerciseApplication: () =>
            {
                _fixture.SetAiMonitoringServerConfigOnConnect(enabled: true, streaming: false, recordContent: false);
                _fixture.Get();
                _fixture.StartAgent();
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentSettingsLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void ServerSideConfigOverridesLocal()
    {
        var enabled = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.enabled");
        var streaming = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.streaming.enabled");
        var recordContent = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.record_content.enabled");

        NrAssert.Multiple(
            () => Assert.True(enabled, "ai_monitoring.enabled should be enabled by server-side config"),
            () => Assert.False(streaming, "ai_monitoring.streaming.enabled should be disabled by server-side config"),
            () => Assert.False(recordContent, "ai_monitoring.record_content.enabled should be disabled by server-side config")
        );
    }
}

// Local HSM on, AIM off locally. Server agent_config tries to enable AIM.
// Expect resolved: enabled=false (local HSM defense-in-depth wins over SSC).
public class AimServerSideConfigIgnoredUnderHsm : NewRelicIntegrationTest<MvcWithCollectorFixture>
{
    private readonly MvcWithCollectorFixture _fixture;

    public AimServerSideConfigIgnoredUnderHsm(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                configModifier.SetLogLevel("finest");
                configModifier.SetHighSecurityMode(true);
                configModifier.EnableAiMonitoring(false);
            },
            exerciseApplication: () =>
            {
                _fixture.SetAiMonitoringServerConfigOnConnect(enabled: true, streaming: true, recordContent: true);
                _fixture.Get();
                _fixture.StartAgent();
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentSettingsLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void HighSecurityModeIgnoresServerSideConfig()
    {
        var enabled = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.enabled");
        var streaming = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.streaming.enabled");
        var recordContent = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.record_content.enabled");

        NrAssert.Multiple(
            () => Assert.False(enabled, "ai_monitoring.enabled must remain disabled under High Security Mode even when server-side config enables it"),
            () => Assert.False(streaming, "ai_monitoring.streaming.enabled must remain disabled under High Security Mode even when server-side config enables it"),
            () => Assert.False(recordContent, "ai_monitoring.record_content.enabled must remain disabled under High Security Mode even when server-side config enables it")
        );
    }
}
