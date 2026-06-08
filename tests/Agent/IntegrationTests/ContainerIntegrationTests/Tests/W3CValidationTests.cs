// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

/// <summary>
/// Runs the official python-based W3C trace-context validation suite
/// (https://github.com/w3c/trace-context/tree/master/test) against a containerized app that has the
/// agent attached, validating that the agent's W3C trace context propagation is spec-compliant.
///
/// This is the containerized replacement for the former IntegrationTests W3CValidation test. Python,
/// aiohttp, and the pinned trace-context repo are baked into the Docker image, so the test host no
/// longer needs Python installed.
///
/// The result is the python process exit code (0 = pass), mirroring the original test. The python
/// suite is triggered via the app's /runtests endpoint and runs in-container against the service.
/// </summary>
public abstract class LinuxW3CValidationTest<T> : NewRelicIntegrationTest<T> where T : W3CValidationTestFixtureBase
{
    private readonly T _fixture;

    protected LinuxW3CValidationTest(T fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("debug");
            },
            exerciseApplication: () =>
            {
                _fixture.ExerciseApplication();

                // shut down the container and wait for the agent log to see it
                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(10));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void Test()
    {
        Assert.True(_fixture.LastExitCode == 0,
            $"Python W3C trace-context validation exited with code {_fixture.LastExitCode}. Check the output for the failure(s):{Environment.NewLine}{_fixture.LastOutput}");
    }
}

[Trait("Architecture", "amd64")]
[Trait("Distro", "Ubuntu")]
public class W3CValidationDotNet10Test : LinuxW3CValidationTest<W3CValidationDotNet10TestFixture>
{
    public W3CValidationDotNet10Test(W3CValidationDotNet10TestFixture fixture, ITestOutputHelper output) : base(fixture, output)
    {
    }
}
