// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class AwsSdkContainerFirehoseTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "resolute";

    private readonly string BaseUrl;

    protected AwsSdkContainerFirehoseTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdkfirehose";
    }

    public void CreateDeliveryStreamAsync(string streamName, string bucketName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/CreateDeliveryStreamAsync?streamName={streamName}&bucketName={bucketName}", System.Net.HttpStatusCode.OK);
    }
    public void DeleteDeliveryStreamAsync(string streamName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/DeleteDeliveryStreamAsync?streamName={streamName}", System.Net.HttpStatusCode.OK);
    }

    public void ListDeliveryStreamsAsync()
    {
        GetAndAssertStatusCode($"{BaseUrl}/ListDeliveryStreamsAsync", System.Net.HttpStatusCode.OK);
    }

    public void PutRecordAsync(string streamName, string data)
    {
        GetAndAssertStatusCode($"{BaseUrl}/PutRecordAsync?streamName={streamName}&data={data}", System.Net.HttpStatusCode.OK);
    }

    public void PutRecordBatchAsync(string streamName, string data)
    {
        GetAndAssertStatusCode($"{BaseUrl}/PutRecordBatchAsync?streamName={streamName}&data={data}", System.Net.HttpStatusCode.OK);
    }

}

public class AwsSdkContainerFirehoseTestFixtureCoreOldest : AwsSdkContainerFirehoseTestFixture
{
    public AwsSdkContainerFirehoseTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class AwsSdkContainerFirehoseTestFixtureCoreLatest : AwsSdkContainerFirehoseTestFixture
{
    public AwsSdkContainerFirehoseTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
