// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public class AwsSdkContainerKinesisTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    private readonly string BaseUrl;

    public AwsSdkContainerKinesisTestFixture() : base(DistroTag, Architecture, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdkkinesis";
    }

    public void CreateStreamAsync(string streamName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/CreateStreamAsync?streamName={streamName}", System.Net.HttpStatusCode.OK);
    }
    public void ListStreamsAsync()
    {
        GetAndAssertStatusCode($"{BaseUrl}/ListStreamsAsync", System.Net.HttpStatusCode.OK);
    }

}
