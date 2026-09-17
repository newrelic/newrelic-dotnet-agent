// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

/// <summary>
/// M5: drives a genuinely FAILING ingest endpoint. The send-failure no-send guard was removed on this branch
/// (see Dockerfile.continuousprofiling), so the agent now sends unconditionally and must degrade gracefully
/// when the collector rejects a profile: log the rejection, keep the session running, and RECOVER once the
/// endpoint is healthy again -- rather than throwing, crashing, or permanently disabling itself. The mock's
/// <c>OtlpProfilesController</c> is switched to return HTTP 500 for every profiles POST (via the
/// <c>/v1/profiles/response-status</c> control endpoint), exercised, then switched back so the recovery is
/// observable. Nothing else covers this path -- the receiver test's mock always returns Ok.
/// </summary>
public class ContinuousProfilingOtlpSendFailureTests : NewRelicIntegrationTest<AspNetCoreWebApiWithCollectorFixture>
{
    private const int SamplingIntervalMs = 1000;
    private const int FailureStatusCode = 500;

    private readonly AspNetCoreWebApiWithCollectorFixture _fixture;

    private bool _rejectionWarningLogged;
    private List<ProfilesSummaryDto> _profilesAfterRecovery = new List<ProfilesSummaryDto>();

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; sampling every (\d+) ms, draining every (\d+) ms\.";

    private static readonly string BuiltProfileLogLineRegex =
        AgentLogBase.DebugLogLinePrefixRegex + @"\[ContinuousProfiling\] Posting profile \((\w+)\); (\d+) bytes to (\S+)\.";

    // The transport's rate-limited Warn for any non-2xx rejection (WarnOnRejectionRateLimited). Always
    // emitted (Warn level) on the first rejection regardless of the configured log level.
    private static readonly string RejectionWarnLogLineRegex =
        AgentLogBase.WarnLogLinePrefixRegex + @"\[ContinuousProfiling\] Profile send rejected with status " + FailureStatusCode;

    public ContinuousProfilingOtlpSendFailureTests(AspNetCoreWebApiWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");

                _fixture.EnvironmentVariables["NEW_RELIC_PROFILING_ENABLED"] = "true";
                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_SAMPLING_INTERVAL_MS"] = SamplingIntervalMs.ToString();
            },
            exerciseApplication: () =>
            {
                // Session starts at agent init; confirm before doing anything else.
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Make the ingest endpoint fail for every POST, THEN generate sampleable work so the drains
                // that carry real profiles hit the failing endpoint.
                _fixture.SetOtlpProfilesResponseStatus(FailureStatusCode);

                // A built profile POST must occur (so the failure path is actually reached), and the agent
                // must log the rejection Warn rather than crashing or going silent.
                _fixture.BurnCpu(8);
                _fixture.AgentLog.WaitForLogLine(BuiltProfileLogLineRegex, TimeSpan.FromMinutes(2));

                _rejectionWarningLogged = TryWaitForLogLine(RejectionWarnLogLineRegex, TimeSpan.FromMinutes(1));

                // Recovery: the session must NOT have disabled itself on failure. Heal the endpoint and prove
                // a subsequent drain succeeds and lands a real profile at the collector.
                _fixture.SetOtlpProfilesResponseStatus(0);
                _fixture.BurnCpu(8);
                _profilesAfterRecovery = WaitForContentValidProfile(TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    private bool TryWaitForLogLine(string regex, TimeSpan timeout)
    {
        try
        {
            _fixture.AgentLog.WaitForLogLine(regex, timeout);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private List<ProfilesSummaryDto> WaitForContentValidProfile(TimeSpan timeout)
    {
        var stopwatch = Stopwatch.StartNew();
        var latest = new List<ProfilesSummaryDto>();
        while (stopwatch.Elapsed < timeout)
        {
            latest = _fixture.GetCollectedOtlpProfiles(100).ToList();
            if (latest.Any(p => p.ContentValid))
            {
                return latest;
            }

            Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        return latest;
    }

    [Fact]
    public void AgentHandlesIngestFailureAndRecovers()
    {
        NrAssert.Multiple(
            // The agent reached the failing endpoint and logged the rejection instead of crashing / going silent.
            () => Assert.True(_rejectionWarningLogged,
                $"Expected a rate-limited rejection warning for HTTP {FailureStatusCode}, but none was logged."),
            // No-send guard is gone: the session kept running and recovered, landing a real profile once the
            // endpoint was healthy again (reaching this point at all also proves the send failures never
            // surfaced into the host -- the app ran through to a clean shutdown).
            () => Assert.NotEmpty(_profilesAfterRecovery),
            () => Assert.Contains(_profilesAfterRecovery, p => p.ContentValid)
        );
    }
}
