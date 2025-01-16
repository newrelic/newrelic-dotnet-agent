// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public class AwsSdkContainerMultiServiceTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    private readonly string BaseUrl;

    public AwsSdkContainerMultiServiceTestFixture() : base(DistroTag, Architecture, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdkmultiservice";
    }

    public void ExerciseMultiService(string tableName, string queueName, string bookName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/CallMultipleServicesAsync?tableName={tableName}&queueName={queueName}&bookName={bookName}", System.Net.HttpStatusCode.OK);
    }
}
