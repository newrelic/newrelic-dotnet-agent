// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text.Json;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class W3CValidationTestFixtureBase : RemoteApplicationFixture
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    protected override int MaxTries => 1;

    public int LastExitCode { get; private set; } = int.MinValue;
    public string LastOutput { get; private set; } = string.Empty;

    protected W3CValidationTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dotnetVersion,
        string dockerComposeFile = "docker-compose-w3c.yml") :
        base(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile, dockerComposeFile, "W3CTestApp"))
    {
    }

    public virtual void ExerciseApplication()
    {
        // Triggers the python W3C trace-context validation suite inside the container and captures its result.
        var address = $"http://localhost:{Port}/runtests";
        var response = GetString(address);

        var result = JsonSerializer.Deserialize<W3CTestRunResult>(response, _jsonOptions);
        LastExitCode = result?.ExitCode ?? int.MinValue;
        LastOutput = result?.Output ?? string.Empty;
    }

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }

    private class W3CTestRunResult
    {
        public int ExitCode { get; set; }

        public string Output { get; set; }
    }
}

public class W3CValidationDotNet10TestFixture : W3CValidationTestFixtureBase
{
    private const string Dockerfile = "W3CTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "noble"; // Ubuntu 24.04
    private const string DotnetVersion = "10.0";

    public W3CValidationDotNet10TestFixture() : base(DistroTag, Architecture, Dockerfile, DotnetVersion) { }
}
