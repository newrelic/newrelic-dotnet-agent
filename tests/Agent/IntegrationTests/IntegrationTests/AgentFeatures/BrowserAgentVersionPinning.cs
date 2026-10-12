// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json.Linq;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

public abstract class BrowserAgentVersionPinningBase : NewRelicIntegrationTest<BasicAspNetCoreRazorApplicationFixture>
{
    private const string VersionSettingKey = "browser_monitoring.version";

    protected readonly BasicAspNetCoreRazorApplicationFixture _fixture;
    protected readonly string _requestedVersion;
    protected string _htmlContent;

    protected BrowserAgentVersionPinningBase(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output, string requestedVersion)
        : base(fixture)
    {
        _requestedVersion = requestedVersion;

        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.AutoInstrumentBrowserMonitoring(true);
                configModifier.EnableAspNetCore6PlusBrowserInjection(true);
                configModifier.BrowserMonitoringVersion(requestedVersion);
            },
            exerciseApplication: () =>
            {
                _htmlContent = _fixture.Get("Index");
            }
        );

        _fixture.Initialize();
    }

    protected string ConnectedVersionSetting()
    {
        var settings = JObject.FromObject(_fixture.AgentLog.GetConnectData().JavascriptAgentSettings);
        return (string)settings[VersionSettingKey];
    }

    protected bool WarningLogged()
    {
        var warning = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WarnLogLinePrefixRegex +
            "browser_monitoring.version is set to '" + _requestedVersion + "' but the collector did not return a browser agent loader");
        return warning != null && warning.Success;
    }
}

public class BrowserAgentVersionPinningResolved : BrowserAgentVersionPinningBase
{
    // A pinned version leaves browser agent support over time. Update it when the collector stops returning it.
    private const string PinnedVersion = "1.322.0";

    public BrowserAgentVersionPinningResolved(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, PinnedVersion)
    {
    }

    [Fact]
    public void PinnedVersionIsRequestedAndInjected()
    {
        var connectResponse = _fixture.AgentLog.GetConnectResponseData();
        var injectedLoader = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);

        NrAssert.Multiple(
            () => Assert.Equal(PinnedVersion, ConnectedVersionSetting()),
            () => Assert.Equal(PinnedVersion, connectResponse.BrowserMonitoringLoaderVersion),
            () => Assert.False(string.IsNullOrEmpty(connectResponse.JsAgentLoader)),
            () => Assert.Equal(connectResponse.JsAgentLoader, injectedLoader),
            () => Assert.False(WarningLogged())
        );
    }
}

public class BrowserAgentVersionPinningUnresolved : BrowserAgentVersionPinningBase
{
    private const string UnavailableVersion = "0.0.1";

    public BrowserAgentVersionPinningUnresolved(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, UnavailableVersion)
    {
    }

    [Fact]
    public void UnavailableVersionIsRequestedAndNotInjected()
    {
        var connectResponse = _fixture.AgentLog.GetConnectResponseData();

        NrAssert.Multiple(
            () => Assert.Equal(UnavailableVersion, ConnectedVersionSetting()),
            () => Assert.True(string.IsNullOrEmpty(connectResponse.JsAgentLoader)),
            () => Assert.Null(JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent)),
            () => Assert.True(WarningLogged())
        );
    }
}
