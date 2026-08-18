// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Diagnostics;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.MultiAppDomain;

public class MultiAppDomainWebApplicationFixture : RemoteApplicationFixture
{
    public const string RootAppReportedName = "MultiAppDomainTestAppRoot";
    public const string SecondAppReportedName = "MultiAppDomainTestAppTwo";

    public const string ExpectedTransactionMetricName = @"WebTransaction/MVC/DefaultController/CustomParameters";

    public const string AppMarkerAttributeName = "appMarker";
    public const string RootAppMarkerValue = "app_one";
    public const string SecondAppMarkerValue = "app_two";

    // Asymmetric on purpose, so a leak cannot hide behind equal counts.
    public const int RootAppRequestCount = 3;
    public const int SecondAppRequestCount = 1;

    // Explicit and DISJOINT. Without them AgentLogFile falls back to the "newrelic_agent_*.log" glob
    // (AgentLogFile.cs:36-46), which silently returns whichever of the two files was written most
    // recently instead of throwing, making every assertion nondeterministic.
    private const string RootAppLogFileNamePattern = "newrelic_agent_*ROOT.log";
    private const string SecondAppLogFileNamePattern = "newrelic_agent_*app2.log";

    private static readonly TimeSpan WarmUpTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan WarmUpRetryInterval = TimeSpan.FromSeconds(5);

    private AgentLogFile _rootAppAgentLog;
    private AgentLogFile _secondAppAgentLog;

    private RemoteMultiAppWebApplication MultiAppApplication => (RemoteMultiAppWebApplication)RemoteApplication;

    public AgentLogFile RootAppAgentLog => _rootAppAgentLog ??
        (_rootAppAgentLog = new AgentLogFile(DestinationNewRelicLogFileDirectoryPath, TestLogger, RootAppLogFileNamePattern, Timing.TimeToWaitForLog));

    public AgentLogFile SecondAppAgentLog => _secondAppAgentLog ??
        (_secondAppAgentLog = new AgentLogFile(DestinationNewRelicLogFileDirectoryPath, TestLogger, SecondAppLogFileNamePattern, Timing.TimeToWaitForLog));

    public MultiAppDomainWebApplicationFixture()
        : base(new RemoteMultiAppWebApplication("BasicMvcApplication", ApplicationType.Bounded))
    {
        // Set BOTH names explicitly. RemoteApplication.AppName otherwise defaults to the
        // CI_NEW_RELIC_APP_NAME environment variable when set and to the literal
        // "IntegrationTestAppName" when not (RemoteApplication.cs:216-235), so the root application's
        // reported name would vary between local and CI runs and assertion 4 could not know it.
        MultiAppApplication.AppName = RootAppReportedName;
        MultiAppApplication.SecondAppName = SecondAppReportedName;

        // Assertion 6 reads the profiler log.
        ProfilerLogExpected = true;
    }

    /// <summary>
    /// Warms an application up on an endpoint that costs no metric: Default/Ignored calls
    /// NewRelic.Api.Agent.NewRelic.IgnoreTransaction() (DefaultController.cs:38-42). This absorbs
    /// ASP.NET compilation and agent initialization so that cold start cannot swallow a counted
    /// request, which would yield 2 instead of 3 and read exactly like a leak.
    /// </summary>
    public void WarmUpRootApp()
    {
        WarmUp($"http://{DestinationServerName}:{Port}/Default/Ignored?data=warmup");
    }

    public void WarmUpSecondApp()
    {
        WarmUp($"http://{DestinationServerName}:{Port}/{MultiAppApplication.SecondAppUrlPath}/Default/Ignored?data=warmup");
    }

    public void GetRootAppCustomAttributes()
    {
        var address = $"http://{DestinationServerName}:{Port}/Default/CustomParameters"
            + $"?key1={AppMarkerAttributeName}&value1={RootAppMarkerValue}&key2=k2&value2=v2";
        GetStringAndAssertContains(address, "Worked");
    }

    public void GetSecondAppCustomAttributes()
    {
        var address = $"http://{DestinationServerName}:{Port}/{MultiAppApplication.SecondAppUrlPath}/Default/CustomParameters"
            + $"?key1={AppMarkerAttributeName}&value1={SecondAppMarkerValue}&key2=k2&value2=v2";
        GetStringAndAssertContains(address, "Worked");
    }

    /// <summary>
    /// Retries rather than issuing one request, following the BasicMvcApplicationTestFixture
    /// .WaitForStartup precedent: a cold ASP.NET Framework application behind Hosted Web Core can
    /// exceed the default HttpClient timeout on its very first request.
    /// </summary>
    private void WarmUp(string address)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < WarmUpTimeout)
        {
            try
            {
                GetStringAndAssertEqual(address, "warmup");
                return;
            }
            catch (Exception)
            {
                Thread.Sleep(WarmUpRetryInterval);
            }
        }

        Assert.Fail($"Application at {address} did not respond within {WarmUpTimeout.TotalMinutes:N0} minutes.");
    }
}
