// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public class AwsSdkContainerFirehoseTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    private readonly string BaseUrl;

    public AwsSdkContainerFirehoseTestFixture() : base(DistroTag, Architecture, Dockerfile)
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
