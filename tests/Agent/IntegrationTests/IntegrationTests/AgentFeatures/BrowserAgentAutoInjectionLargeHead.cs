// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

// Regression test for double RUM injection in the ASP.NET Core 6+ browser injector
// (GitHub issue #3725).
//
// When the <head> section of an HTML response is larger than the response writer's internal
// buffer, the response body reaches the wrapped stream across more than one Write/WriteAsync
// call. BrowserInjectingStreamWrapper evaluates each write independently, so without a
// once-per-response latch the script is injected twice: the first write contains the <head>
// anchor, and a later write contains "<body" but no "<head", so the FindIndexBeforeBodyTag
// fallback fires and injects again. Writes containing neither anchor are passed through
// untouched, so the count tops out at two. The .NET Framework path was never affected -
// BrowserMonitoringStreamInjector switches to PassThroughStreamWriter after the first
// successful injection.
//
// Run both with and without response compression. With compression enabled there are TWO
// BrowserInjectingStreamWrapper instances for one response - the middleware wraps the response
// body, and ResponseCompressionBodyOnWriteWrapper separately wraps the compression stream - so
// the compressed variant covers the multi-write path through that second instance, which no
// existing test did. The latch lives in HttpContext.Items so both instances observe it.
public abstract class BrowserAgentAutoInjectionLargeHeadBase : NewRelicIntegrationTest<BasicAspNetCoreRazorApplicationFixture>
{
    private readonly BasicAspNetCoreRazorApplicationFixture _fixture;
    private string _htmlContent;

    protected BrowserAgentAutoInjectionLargeHeadBase(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output, bool enableResponseCompression)
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
                _htmlContent = _fixture.Get("LargeHead");
            }
        );

        _fixture.SetResponseCompression(enableResponseCompression);

        _fixture.Initialize();
    }

    [Fact]
    public void RumScriptIsInjectedOnlyOnce()
    {
        // This test project is xUnit, so NrAssert.Multiple (which delegates to NUnit's
        // Assert.Multiple) cannot aggregate these failures - the first failing assert throws.
        // They are ordered so an infrastructure problem surfaces as its own distinct failure
        // before the interesting count assertion.
        Assert.NotNull(_htmlContent);

        var connectResponseData = _fixture.AgentLog.GetConnectResponseData();
        var jsAgentFromConnectResponse = connectResponseData.JsAgentLoader;
        var jsAgentFromHtmlContent = JavaScriptAgent.GetJavaScriptAgentScriptFromSource(_htmlContent);

        // confirm injection happened at all
        Assert.Equal(jsAgentFromConnectResponse, jsAgentFromHtmlContent);

        // verify that the browser injecting stream wrapper didn't catch an exception and disable itself
        var agentDisabledLogLine = _fixture.AgentLog.TryGetLogLine(AgentLogBase.ErrorLogLinePrefixRegex + "Unexpected exception. Browser injection will be disabled. *?");
        Assert.Null(agentDisabledLogLine);

        // the interesting assertion: the RUM loader marker should appear exactly once.
        // More than one occurrence means the agent injected the browser script multiple
        // times into a single response. The failure message reports every occurrence's
        // offset and which region of the document it falls in, so a regression tells us
        // where the extra injection landed instead of only that it happened.
        var markerOffsets = JavaScriptAgent.FindInfoBlockOffsets(_htmlContent);

        var openHeadIndex = _htmlContent.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        var openHeadEndIndex = openHeadIndex >= 0 ? _htmlContent.IndexOf('>', openHeadIndex) : -1;
        var closeHeadIndex = _htmlContent.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        var bodyOpenIndex = _htmlContent.IndexOf("<body", StringComparison.OrdinalIgnoreCase);

        var detail = new StringBuilder();
        for (var i = 0; i < markerOffsets.Count; i++)
        {
            if (i > 0)
            {
                detail.Append(", ");
            }

            var offset = markerOffsets[i];
            detail.Append(offset).Append(" (").Append(ClassifyRegion(offset, openHeadEndIndex, closeHeadIndex, bodyOpenIndex)).Append(')');
        }

        var message = $"Expected the RUM script to be injected exactly once; found {markerOffsets.Count} " +
            $"NREUM.info occurrence(s). Offsets/regions: {detail}. Anchors: openHeadEnd={openHeadEndIndex}, " +
            $"closeHead={closeHeadIndex}, bodyOpen={bodyOpenIndex}, contentLength={_htmlContent.Length}.";

        Assert.True(markerOffsets.Count == 1, message);
    }

    // Classifies an offset relative to the document's <head>/<body> anchors. Anchors
    // that were not found (-1) are handled defensively so this never throws while
    // building an assertion failure message.
    private static string ClassifyRegion(int offset, int openHeadEndIndex, int closeHeadIndex, int bodyOpenIndex)
    {
        if (openHeadEndIndex >= 0 && offset > openHeadEndIndex && (closeHeadIndex < 0 || offset < closeHeadIndex))
        {
            return "in-head";
        }

        if (closeHeadIndex >= 0 && offset > closeHeadIndex && (bodyOpenIndex < 0 || offset < bodyOpenIndex))
        {
            return "between-head-and-body";
        }

        if (bodyOpenIndex >= 0 && offset >= bodyOpenIndex)
        {
            return "in-body";
        }

        return "other/before-head";
    }
}

public class BrowserAgentAutoInjectionLargeHeadUnCompressed : BrowserAgentAutoInjectionLargeHeadBase
{
    public BrowserAgentAutoInjectionLargeHeadUnCompressed(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, false)
    {
    }
}

public class BrowserAgentAutoInjectionLargeHeadCompressed : BrowserAgentAutoInjectionLargeHeadBase
{
    public BrowserAgentAutoInjectionLargeHeadCompressed(BasicAspNetCoreRazorApplicationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, true)
    {
    }
}
