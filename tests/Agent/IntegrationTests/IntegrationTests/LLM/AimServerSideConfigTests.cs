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
[Trait("Platform", "WindowsOnly")]
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

// Local HSM on, no local AIM setting (basic telemetry is on by default). Server agent_config tries to enable AIM.
// Expect resolved: enabled=false (local HSM defense-in-depth wins over SSC and over the default).
[Trait("Platform", "WindowsOnly")]
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

// Local basic telemetry on, no local ai_monitoring.enabled. Server agent_config turns basic telemetry off.
// Expect resolved: basic_telemetry=false and AIM inactive (enabled=false, record_content=false).
[Trait("Platform", "WindowsOnly")]
public class AimServerSideBasicTelemetryOffDisablesAim : NewRelicIntegrationTest<MvcWithCollectorFixture>
{
    private readonly MvcWithCollectorFixture _fixture;

    public AimServerSideBasicTelemetryOffDisablesAim(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                configModifier.SetLogLevel("finest");
                configModifier.EnableAiMonitoringBasicTelemetry(true);
            },
            exerciseApplication: () =>
            {
                _fixture.SetAiMonitoringServerConfigOnConnect(basicTelemetry: false);
                _fixture.Get();
                _fixture.StartAgent();
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentSettingsLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void ServerSideBasicTelemetryOffDisablesAim()
    {
        var basicTelemetry = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.basic_telemetry.enabled");
        var enabled = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.enabled");
        var recordContent = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.record_content.enabled");

        NrAssert.Multiple(
            () => Assert.False(basicTelemetry, "ai_monitoring.basic_telemetry.enabled should be disabled by server-side config"),
            () => Assert.False(enabled, "ai_monitoring.enabled should be inactive when server-side config disables basic telemetry and enabled is unset"),
            () => Assert.False(recordContent, "ai_monitoring.record_content.enabled should be inactive when AI monitoring is inactive")
        );
    }
}

// Local basic telemetry off, no local ai_monitoring.enabled. Server agent_config turns basic telemetry on.
// Expect resolved: basic_telemetry=true, AIM active without content (enabled=true, record_content=false).
[Trait("Platform", "WindowsOnly")]
public class AimServerSideBasicTelemetryOnEnablesAimWithoutContent : NewRelicIntegrationTest<MvcWithCollectorFixture>
{
    private readonly MvcWithCollectorFixture _fixture;

    public AimServerSideBasicTelemetryOnEnablesAimWithoutContent(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(_fixture.DestinationNewRelicConfigFilePath, new[] { "configuration", "service" }, "autoStart", "false");
                configModifier.SetLogLevel("finest");
                configModifier.EnableAiMonitoringBasicTelemetry(false);
            },
            exerciseApplication: () =>
            {
                _fixture.SetAiMonitoringServerConfigOnConnect(basicTelemetry: true);
                _fixture.Get();
                _fixture.StartAgent();
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentSettingsLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );
        _fixture.Initialize();
    }

    [Fact]
    public void ServerSideBasicTelemetryOnEnablesAimWithoutContent()
    {
        var basicTelemetry = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.basic_telemetry.enabled");
        var enabled = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.enabled");
        var recordContent = _fixture.AgentLog.GetReportedAiMonitoringSetting("ai_monitoring.record_content.enabled");

        NrAssert.Multiple(
            () => Assert.True(basicTelemetry, "ai_monitoring.basic_telemetry.enabled should be enabled by server-side config"),
            () => Assert.True(enabled, "ai_monitoring.enabled should be active when server-side config enables basic telemetry and enabled is unset"),
            () => Assert.False(recordContent, "ai_monitoring.record_content.enabled should be off when neither enabled nor record_content is set to true")
        );
    }
}
