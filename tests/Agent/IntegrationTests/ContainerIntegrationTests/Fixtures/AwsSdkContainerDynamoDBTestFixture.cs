// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.ContainerIntegrationTests.Applications;
using NewRelic.Agent.IntegrationTestHelpers;

namespace NewRelic.Agent.ContainerIntegrationTests.Fixtures;

public abstract class AwsSdkContainerDynamoDBTestFixture : AwsSdkContainerTestFixtureBase
{
    private const string Dockerfile = "AwsSdkTestApp/Dockerfile";
    private const ContainerApplication.Architecture Architecture = ContainerApplication.Architecture.X64;
    private const string DistroTag = "resolute";

    private readonly string BaseUrl;

    protected AwsSdkContainerDynamoDBTestFixture(string dotnetVersion) : base(DistroTag, Architecture, dotnetVersion, Dockerfile)
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

public class AwsSdkContainerDynamoDBTestFixtureCoreOldest : AwsSdkContainerDynamoDBTestFixture
{
    public AwsSdkContainerDynamoDBTestFixtureCoreOldest() : base(Tfm.NetOldestVersion) { }
}

public class AwsSdkContainerDynamoDBTestFixtureCoreLatest : AwsSdkContainerDynamoDBTestFixture
{
    public AwsSdkContainerDynamoDBTestFixtureCoreLatest() : base(Tfm.NetLatestVersion) { }
}
