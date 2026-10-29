// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

public abstract class BrowserAgentAutoInjection6PlusBase<TFixture> : NewRelicIntegrationTest<TFixture>
    where TFixture : BasicAspNetCoreRazorApplicationFixture
{
    private readonly TFixture _fixture;
    private string _htmlContent;
    private string _staticContent;
    private string _fooContent;
    private bool _browserInjectionEnabled;

    protected BrowserAgentAutoInjection6PlusBase(TFixture fixture,
        ITestOutputHelper output, bool enableBrowserInjection, bool enableResponseCompression, string loaderType = "rum")
        : base(fixture)
    {
        _browserInjectionEnabled = enableBrowserInjection;

        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configPath = fixture.DestinationNewRelicConfigFilePath;

                var configModifier = new NewRelicConfigModifier(configPath);
                configModifier.AutoInstrumentBrowserMonitoring(true);
                configModifier.BrowserMonitoringEnableAttributes(true);
                configModifier.EnableAspNetCore6PlusBrowserInjection(enableBrowserInjection);

                configModifier.BrowserMonitoringLoader(loaderType);
            },
            exerciseApplication: () =>
            {
                _htmlContent = _fixture.Get("Index"); // get a razor page
                _staticContent = _fixture.Get("static.html"); // get static content
                _fooContent = _fixture.Get("foo"); // get a manually-written response body with no content type
            }
        );

        _fixture.SetResponseCompression(enableResponseCompression);

        _fixture.Initialize();

    }

    [Fact]
    public void Test()
    {
        NrAssert.Multiple(
            () => Assert.NotNull(_htmlContent),
            () => Assert.NotNull(_staticContent),
            () => Assert.NotNull(_fooContent)
        );

        var connectResponseData = _fixture.AgentLog.GetConnectResponseData();

        var jsAgentFromConnectResponse = connectResponseData.JsAgentLoader;

        var jsAgentFromHtmlContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);
        var jsAgentFromStaticContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_staticContent);
        var jsAgentFromFooContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_fooContent);

        if (_browserInjectionEnabled)
        {
            Assert.Equal(jsAgentFromConnectResponse, jsAgentFromHtmlContent);
            Assert.Equal(jsAgentFromConnectResponse, jsAgentFromStaticContent);
            Assert.Null(jsAgentFromFooContent);
        }
        else
        {
            Assert.Null(jsAgentFromHtmlContent);
            Assert.Null(jsAgentFromStaticContent);
            Assert.Null(jsAgentFromFooContent);
        }

        // verify that the browser injecting stream wrapper didn't catch an exception and disable itself
        var agentDisabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.ErrorLogLinePrefixRegex + "Unexpected exception. Browser injection will be disabled. *?");
        Assert.Null(agentDisabledLogLine);

        var expectedMetric = new Assertions.ExpectedMetric { metricName = $@"Supportability/Dotnet/AspNetCore6PlusBrowserInjection/{(_browserInjectionEnabled ? "enabled" : "disabled")}" };
        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        Assertions.MetricExists(expectedMetric, metrics);

    }
}

public class BrowserAgentAutoInjection6PlusRumUnCompressedCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusRumUnCompressedCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, false)
    {
    }
}

public class BrowserAgentAutoInjection6PlusRumUnCompressedCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusRumUnCompressedCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, false)
    {
    }
}

public class BrowserAgentAutoInjection6PlusRumCompressedCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusRumCompressedCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true)
    {
    }
}

public class BrowserAgentAutoInjection6PlusRumCompressedCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusRumCompressedCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true)
    {
    }
}

public class BrowserAgentAutoInjection6PlusInjectionDisabledRumUnCompressedCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusInjectionDisabledRumUnCompressedCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, false)
    {
    }
}

public class BrowserAgentAutoInjection6PlusInjectionDisabledRumUnCompressedCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusInjectionDisabledRumUnCompressedCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, false)
    {
    }
}

public class BrowserAgentAutoInjection6PlusInjectionDisabledRumCompressedCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusInjectionDisabledRumCompressedCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, false, true)
    {
    }
}

public class BrowserAgentAutoInjection6PlusInjectionDisabledRumCompressedCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusInjectionDisabledRumCompressedCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, false, true)
    {
    }
}

public class BrowserAgentAutoInjection6PlusSpaCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusSpaCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true, "spa")
    {
    }
}

public class BrowserAgentAutoInjection6PlusSpaCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusSpaCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true, "spa")
    {
    }
}

public class BrowserAgentAutoInjection6PlusFullCoreOldest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreOldest>
{
    public BrowserAgentAutoInjection6PlusFullCoreOldest(BasicAspNetCoreRazorApplicationFixtureCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true, "full")
    {
    }
}

public class BrowserAgentAutoInjection6PlusFullCoreLatest : BrowserAgentAutoInjection6PlusBase<BasicAspNetCoreRazorApplicationFixtureCoreLatest>
{
    public BrowserAgentAutoInjection6PlusFullCoreLatest(BasicAspNetCoreRazorApplicationFixtureCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, true, true, "full")
    {
    }
}
