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
    //public void DeleteStreamAsync(string streamName)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/DeleteStreamAsync?streamName={streamName}", System.Net.HttpStatusCode.OK);
    //}

    //public void ListStreamsAsync()
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/ListStreamsAsync", System.Net.HttpStatusCode.OK);
    //}

    //public void RegisterStreamConsumerAsync(string streamName, string consumerName)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/RegisterStreamConsumerAsync?streamName={streamName}&consumerName={consumerName}", System.Net.HttpStatusCode.OK);
    //}

    //public void DeregisterStreamConsumerAsync(string streamName, string consumerName)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/DeregisterStreamConsumerAsync?streamName={streamName}&consumerName={consumerName}", System.Net.HttpStatusCode.OK);
    //}

    //public void ListStreamConsumersAsync(string streamNAme)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/ListStreamConsumersAsync?streamName={streamNAme}", System.Net.HttpStatusCode.OK);
    //}

    //public void PutRecordAsync(string streamName, string data)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/PutRecordAsync?streamName={streamName}&data={data}", System.Net.HttpStatusCode.OK);
    //}

    //public void PutRecordsAsync(string streamName, string data)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/PutRecordsAsync?streamName={streamName}&data={data}", System.Net.HttpStatusCode.OK);
    //}

    //public void GetRecordsAsync(string streamName)
    //{
    //    GetAndAssertStatusCode($"{BaseUrl}/GetRecordsAsync?streamName={streamName}", System.Net.HttpStatusCode.OK);
    //}
}
