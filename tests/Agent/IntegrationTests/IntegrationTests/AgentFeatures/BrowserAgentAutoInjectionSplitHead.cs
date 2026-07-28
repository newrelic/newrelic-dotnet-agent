// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

// Regression test for silent response-content loss in the ASP.NET Core 6+ browser injector.
//
// BrowserScriptInjectionHelper.InjectBrowserScriptAsync used to reject an injection index
// equal to the buffer length as invalid: it logged "Skipping RUM Injection: Insertion index
// was invalid." and returned WITHOUT writing the buffer at all. BrowserInjectingStreamWrapper
// returns immediately after calling into the agent and never writes the buffer itself, so
// that write's bytes never reached the client - the response was silently truncated. An index
// equal to the buffer length is now handled as a valid split point: the script is appended and
// the trailing write covers zero bytes.
//
// That index arises when a single Write/WriteAsync call's buffer ends exactly on the closing
// '>' of the opening <head> tag. Forcing an ASP.NET Core response writer's internal buffer
// boundary to land there is not reliably controllable, so the test app's "/splithead" endpoint
// writes the response in two explicit WriteAsync calls split at exactly that byte.
//
// This is a separate defect from the double injection covered by
// BrowserAgentAutoInjectionLargeHead: that one is about the script being inserted twice, this
// one about response bytes being dropped. Before the fix the response came back starting with
// "</head><script ...NREUM.info..." - the first write's bytes were gone and the script had
// been injected against the second write's <body> anchor instead.
public class BrowserAgentAutoInjectionSplitHead : NewRelicIntegrationTest<BasicAspNetCoreRazorApplicationFixture>
{
    private const string FirstWriteContent = "<html><head>";

    private readonly BasicAspNetCoreRazorApplicationFixture _fixture;
    private string _htmlContent;

    public BrowserAgentAutoInjectionSplitHead(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;
        _fixture.Actions
        (
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                configModifier.AutoInstrumentBrowserMonitoring(true);
                configModifier.BrowserMonitoringEnableAttributes(true);
                configModifier.EnableAspNetCore6PlusBrowserInjection(true);
                configModifier.BrowserMonitoringLoader("rum");
            },
            exerciseApplication: () =>
            {
                _htmlContent = _fixture.Get("splithead");
            }
        );

        _fixture.SetResponseCompression(false);

        _fixture.Initialize();
    }

    [Fact]
    public void ResponseIsNotTruncatedAndScriptIsInjectedOnce()
    {
        // This test project is xUnit, so NrAssert.Multiple (which delegates to NUnit's
        // Assert.Multiple) cannot aggregate these failures - the first failing assert throws.
        // They are ordered so an infrastructure problem surfaces as its own distinct failure
        // before the interesting assertions.
        Assert.NotNull(_htmlContent);

        // THE KEY ASSERTION - no content loss.
        // Using StartsWith (not Contains) because the first write's bytes, if present at all,
        // must be at the very start of the response - nothing could legitimately precede them.
        // If the first write is dropped, the response instead starts with the second write's
        // content ("</head><body>...") or with the RUM script.
        var preview = _htmlContent.Length > 120 ? _htmlContent.Substring(0, 120) : _htmlContent;
        Assert.True(_htmlContent.StartsWith(FirstWriteContent, StringComparison.Ordinal),
            $"Expected the response to start with the first write's content (\"{FirstWriteContent}\") but the " +
            $"first write's bytes appear to have been dropped. First 120 characters of actual response: " +
            $"\"{preview}\"");

        // confirm injection happened at all, and that the injected script is the one the
        // collector handed us
        var jsAgentFromConnectResponse = _fixture.AgentLog.GetConnectResponseData().JsAgentLoader;
        var jsAgentFromHtmlContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);
        Assert.Equal(jsAgentFromConnectResponse, jsAgentFromHtmlContent);

        // The RUM script should still be injected exactly once.
        var markerOffsets = JavaScriptAgent.FindInfoBlockOffsets(_htmlContent);
        Assert.True(markerOffsets.Count == 1,
            $"Expected the RUM script to be injected exactly once; found {markerOffsets.Count} " +
            $"NREUM.info occurrence(s) at offset(s) {string.Join(", ", markerOffsets)} in the response.");

        // Verify that the browser injecting stream wrapper didn't catch an exception and disable itself.
        var agentDisabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.ErrorLogLinePrefixRegex + "Unexpected exception. Browser injection will be disabled. *?");
        Assert.Null(agentDisabledLogLine);
    }
}
