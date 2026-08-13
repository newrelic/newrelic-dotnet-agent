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
/// Exercises the start_continuous_profiler/stop_continuous_profiler agent commands end to end through
/// <see cref="MockNewRelicFixture"/> (<c>TriggerStartContinuousProfiler</c>/<c>TriggerStopContinuousProfiler</c>),
/// which queue a real <c>AgentCommand</c> for the agent's own <c>get_agent_commands</c> poll to pick up --
/// unlike the unit tests for <c>StartContinuousProfilerCommand</c>/<c>ContinuousProfilingService</c>, which
/// call the command/service classes directly in-process.
///
/// Continuous profiling is left disabled in local config here (no
/// <c>NEW_RELIC_CONTINUOUS_PROFILING_ENABLED</c>) so a session starting is proof the command -- not local
/// config -- turned it on, mirroring how <c>ContinuousProfilingThreadProfilingMutualExclusionTests</c>
/// isolates the collector-driven path from config-driven CP behavior.
/// </summary>
public class ContinuousProfilerAgentCommandTests : NewRelicIntegrationTest<AspNetCoreWebApiWithCollectorFixture>
{
    private readonly AspNetCoreWebApiWithCollectorFixture _fixture;
    private int _commandAcksSent;

    private static readonly string SessionStartedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session started; draining every (\d+) ms\.";

    private static readonly string SessionStoppedLogLineRegex =
        AgentLogBase.InfoLogLinePrefixRegex + @"\[ContinuousProfiling\] Session stopped\.";

    public ContinuousProfilerAgentCommandTests(AspNetCoreWebApiWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.AddActions(
            setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterGetAgentCommandsCycle(10);
                // Deliberately no NEW_RELIC_CONTINUOUS_PROFILING_ENABLED -- CP must start from the command alone.
            },
            exerciseApplication: () =>
            {
                _fixture.Get();

                _fixture.TriggerStartContinuousProfiler(include: "cpu");
                _fixture.AgentLog.WaitForLogLine(SessionStartedLogLineRegex, TimeSpan.FromMinutes(1));

                _fixture.TriggerStopContinuousProfiler(include: "cpu");
                _fixture.AgentLog.WaitForLogLine(SessionStoppedLogLineRegex, TimeSpan.FromMinutes(1));

                // Must read this back from the mock collector here, inside exerciseApplication --
                // MockNewRelicFixture.Initialize() shuts the mock collector process down immediately after
                // exerciseApplication returns (it waits for the agent's shutdown log line, then calls
                // MockNewRelicApplication.Shutdown()), so a [Fact] method calling GetCollectedRequests()
                // always hits a torn-down port.
                _commandAcksSent = _fixture.GetCollectedRequests()
                    .Count(r => r.Querystring.Any(qs => qs.Key == "method" && qs.Value == "agent_command_results"));
            }
        );

        _fixture.Initialize();
    }

    [Fact]
    public void StartAndStopCommandsControlContinuousProfilingSession()
    {
        var sessionStarted = _fixture.AgentLog.TryGetLogLines(SessionStartedLogLineRegex).Any();
        var sessionStopped = _fixture.AgentLog.TryGetLogLines(SessionStoppedLogLineRegex).Any();

        NrAssert.Multiple(
            () => Assert.True(sessionStarted, "start_continuous_profiler did not start a continuous profiling session."),
            () => Assert.True(sessionStopped, "stop_continuous_profiler did not stop the continuous profiling session."),
            () => Assert.True(_commandAcksSent >= 2, $"Expected at least 2 agent_command_results posts (start + stop), got {_commandAcksSent}.")
        );
    }
}
