// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class AwsSdkContainerMultiServiceTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "resolute";

    private readonly string BaseUrl;

    protected AwsSdkContainerMultiServiceTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdkmultiservice";
    }

    public void ExerciseMultiService(string tableName, string queueName, string bookName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/CallMultipleServicesAsync?tableName={tableName}&queueName={queueName}&bookName={bookName}", System.Net.HttpStatusCode.OK);
    }
}

public class AwsSdkContainerMultiServiceTestFixtureCoreOldest : AwsSdkContainerMultiServiceTestFixture
{
    public AwsSdkContainerMultiServiceTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class AwsSdkContainerMultiServiceTestFixtureCoreLatest : AwsSdkContainerMultiServiceTestFixture
{
    public AwsSdkContainerMultiServiceTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
