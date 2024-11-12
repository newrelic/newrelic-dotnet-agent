// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures
{
    public abstract class AwsSdkContainerTestFixtureBase(
        string distroTag,
        ContainerApplication.Architecture containerArchitecture,
        string dockerfile,
        string dockerComposeFile = "docker-compose-awssdk.yml") 
        : RemoteApplicationFixture(new ContainerApplication(distroTag, containerArchitecture, DotnetVersion, dockerfile,
            dockerComposeFile, "awssdktestapp"))
    {
        private const string DotnetVersion = "8.0";

        protected override int MaxTries => 1;

        public void Delay(int seconds)
        {
            Task.Delay(TimeSpan.FromSeconds(seconds)).GetAwaiter().GetResult();
        }
    }
}

public class AwsSdkContainerSQSTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    private readonly string BaseUrl;

    public AwsSdkContainerSQSTestFixture() : base(DistroTag, Architecture, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdksqs";
    }

    public void ExerciseSQS_SendReceivePurge(string queueName)
    {
        // The exerciser will return a 500 error if the `RequestMessage.MessageAttributeNames` collection is modified by our instrumentation.
        // See https://github.com/newrelic/newrelic-dotnet-agent/pull/2646 
        GetAndAssertStatusCode($"{BaseUrl}/SQS_SendReceivePurge?queueName={queueName}", System.Net.HttpStatusCode.OK);
    }

    public string ExerciseSQS_SendAndReceiveInSeparateTransactions(string queueName)
    {
        var queueUrl = GetString($"{BaseUrl}/SQS_InitializeQueue?queueName={queueName}");

        GetAndAssertStatusCode($"{BaseUrl}/SQS_SendMessageToQueue?message=Hello&messageQueueUrl={queueUrl}", System.Net.HttpStatusCode.OK);

        var messagesJson = GetString($"{BaseUrl}/SQS_ReceiveMessageFromQueue?messageQueueUrl={queueUrl}");

        GetAndAssertStatusCode($"{BaseUrl}/SQS_DeleteQueue?messageQueueUrl={queueUrl}", System.Net.HttpStatusCode.OK);

        return messagesJson;
    }

    public string ExerciseSQS_ReceiveEmptyMessage(string queueName)
    {
        var queueUrl = GetString($"{BaseUrl}/SQS_InitializeQueue?queueName={queueName}");

        var messagesJson = GetString($"{BaseUrl}/SQS_ReceiveMessageFromQueue?messageQueueUrl={queueUrl}");

        GetAndAssertStatusCode($"{BaseUrl}/SQS_DeleteQueue?messageQueueUrl={queueUrl}", System.Net.HttpStatusCode.OK);

        return messagesJson;
    }

}

public class AwsSdkContainerDynamoDBTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "jammy";

    private readonly string BaseUrl;

    public AwsSdkContainerDynamoDBTestFixture() : base(DistroTag, Architecture, Dockerfile)
    {
        BaseUrl = $"http://localhost:{Port}/awssdkdynamodb";
    }

    public void CreateTableAsync(string tableName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/CreateTableAsync?tableName={tableName}", System.Net.HttpStatusCode.OK);
    }
    public void DeleteTableAsync(string tableName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/DeleteTableAsync?tableName={tableName}", System.Net.HttpStatusCode.OK);
    }

    public void PutItemAsync(string tableName, string title, string year)
    {
        GetAndAssertStatusCode($"{BaseUrl}/PutItemAsync?tableName={tableName}&title={title}&year={year}", System.Net.HttpStatusCode.OK);
    }
    public void GetItemAsync(string tableName, string title, string year)
    {
        GetAndAssertStatusCode($"{BaseUrl}/GetItemAsync?tableName={tableName}&title={title}&year={year}", System.Net.HttpStatusCode.OK);
    }
    public void UpdateItemAsync(string tableName, string title, string year)
    {
        GetAndAssertStatusCode($"{BaseUrl}/UpdateItemAsync?tableName={tableName}&title={title}&year={year}", System.Net.HttpStatusCode.OK);
    }

    public void DeleteItemAsync(string tableName, string title, string year)
    {
        GetAndAssertStatusCode($"{BaseUrl}/DeleteItemAsync?tableName={tableName}&title={title}&year={year}", System.Net.HttpStatusCode.OK);
    }
    public void QueryAsync(string tableName, string title, string year)
    {
        GetAndAssertStatusCode($"{BaseUrl}/QueryAsync?tableName={tableName}&title={title}&year={year}", System.Net.HttpStatusCode.OK);
    }
    public void ScanAsync(string tableName)
    {
        GetAndAssertStatusCode($"{BaseUrl}/ScanAsync?tableName={tableName}", System.Net.HttpStatusCode.OK);
    }

}
