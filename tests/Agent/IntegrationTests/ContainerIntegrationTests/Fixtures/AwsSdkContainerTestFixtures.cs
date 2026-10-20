// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class AwsSdkContainerTestFixtureBase(
    string distroTag,
    ContainerApplication.Architecture containerArchitecture,
    string dotnetVersion,
    string dockerfile,
    string dockerComposeFile = "docker-compose-awssdk.yml")
    : RemoteApplicationFixture(new ContainerApplication(distroTag, containerArchitecture, dotnetVersion, dockerfile,
        dockerComposeFile, "awssdktestapp"))
{
    protected override int MaxTries => 1;

    public void Delay(int seconds)
    {
        Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
    }
}
