// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures;

/// <summary>
/// Continuous profiling and thread profiling are mutually exclusive: <c>ThreadProfilingService</c> has a
/// forward guard that refuses to start a thread-profiling session while continuous profiling is active.
///
/// Unlike the console-app-based <c>ContinuousProfilingTests</c>, this test uses the
/// <see cref="AspNetCoreWebApiWithCollectorFixture"/> (a MockNewRelic-backed fixture, same runtime family
/// -- net10.0 -- as the other CP coverage) so it can actually deliver a collector-driven
/// <c>start_profiler</c> command (<c>MockNewRelicFixture.TriggerThreadProfile</c>) while continuous
/// profiling is running. A console app talking to the real staging collector has no way to trigger that
/// command at all, which is what made the previous version of this assertion vacuously true regardless of
/// whether the guard worked: with no thread-profiling start ever attempted, "no thread-profiling session
/// started" was trivially satisfied whether or not the guard existed.
///
/// Continuous profiling starts at agent init (<c>AgentManager.Start</c>, before
/// <c>AttemptAutoStart</c>/connect -- and before <c>ThreadProfilingService</c> even exists or is wired to
/// the CP session-control reference), so it is already active well before this test's <c>start_profiler</c>
/// command -- which requires a connect plus a further agent-commands poll -- can possibly be delivered and
/// processed. The forward guard is therefore genuinely exercised on every run, not just observed as absent.
/// </summary>
public class ContinuousProfilingThreadProfilingMutualExclusionTests : NewRelicIntegrationTest<AspNetCoreWebApiWithCollectorFixture>
{
    private readonly AspNetCoreWebApiWithCollectorFixture _fixture;

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; draining every (\d+) ms\.";

    // Emitted by the thread profiler's forward guard when a thread-profiling start is attempted while CP is on.
    private static readonly string ThreadProfilingRefusedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"Thread profiling start refused: continuous profiling is active\.";

    // Whichever of the two fires first tells us the start_profiler command has been processed one way or
    // the other, so the test can stop waiting and assert on the outcome.
    private static readonly string ThreadProfilingAttemptResolvedLogLineRegex =
        $"(?:{ThreadProfilingRefusedLogLineRegex})|(?:{AgentLogBase.ThreadProfileStartingLogLineRegex})";

    public ContinuousProfilingThreadProfilingMutualExclusionTests(AspNetCoreWebApiWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterGetAgentCommandsCycle(10);

                _fixture.EnvironmentVariables["NEW_RELIC_CONTINUOUS_PROFILING_ENABLED"] = "true";
            },
            exerciseApplication: () =>
            {
                _fixture.Get();

                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                // Actually attempt to start a thread-profiling session while continuous profiling is
                // active -- this exercises ThreadProfilingService's forward guard for real, rather than
                // merely observing that no attempt happened to occur.
                _fixture.TriggerThreadProfile();

                _fixture.AgentLog.WaitForLogLine(ThreadProfilingAttemptResolvedLogLineRegex, TimeSpan.FromMinutes(1));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void ThreadProfilingStartIsRefusedWhileContinuousProfilingIsActive()
    {
        var sessionStarted = _fixture.AgentLog.TryGetLogLines(SessionStartedLogLineRegex).Any();
        var threadProfilingStarted = _fixture.AgentLog.TryGetLogLines(AgentLogBase.ThreadProfileStartingLogLineRegex).Any();
        var refusalLogged = _fixture.AgentLog.TryGetLogLines(ThreadProfilingRefusedLogLineRegex).Any();

        NrAssert.Multiple(
            () => Assert.True(sessionStarted, "Continuous profiling session never started."),
            () => Assert.True(refusalLogged, "The thread-profiling start attempt was not refused while continuous profiling was active."),
            () => Assert.False(threadProfilingStarted, "A thread-profiling session started while continuous profiling was active.")
        );
    }
}
