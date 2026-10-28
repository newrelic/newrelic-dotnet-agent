// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.OpenTelemetryBridge.Tracing;
using NewRelic.Agent.Core.Segments;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge;

// Regression tests for NR-626783: server.port sent as long must not be reported as 0.
[TestFixture]
public class ActivityBridgeSegmentHelpersPortTests
{
    public interface ITestSegment : ISegment, ISegmentExperimental, IHybridAgentSegment { }

    public interface ITestAgent : IAgent, IAgentExperimental { }

    private const long Port = 27018L;

    private ActivitySource _activitySource;
    private ActivityListener _activityListener;
    private ITestSegment _segment;
    private ITestAgent _agent;
    private ITransaction _transaction;
    private ISegmentData _segmentData;

    [SetUp]
    public void SetUp()
    {
        _activitySource = new ActivitySource(nameof(ActivityBridgeSegmentHelpersPortTests));
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == _activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_activityListener);

        _segmentData = null;
        _segment = Mock.Create<ITestSegment>();
        Mock.Arrange(() => _segment.SetSegmentData(Arg.IsAny<ISegmentData>()))
            .DoInstead((ISegmentData data) => _segmentData = data)
            .Returns(_segment);

        _transaction = Mock.Create<ITransaction>();
        Mock.Arrange(() => _segment.GetTransactionFromSegment()).Returns(_transaction);

        _agent = Mock.Create<ITestAgent>();
    }

    [TearDown]
    public void TearDown()
    {
        _activityListener.Dispose();
        _activitySource.Dispose();
    }

    [Test]
    public void DefaultDatastore_Should_Read_Long_Server_Port()
    {
        ProcessActivity("find", ActivityKind.Client,
            ("db.system.name", "mongodb"), ("db.query.text", "find"), ("server.address", "localhost"), ("server.port", Port));

        Assert.That(((DatastoreSegmentData)_segmentData).Port, Is.EqualTo(Port));
    }

    [Test]
    public void ElasticsearchDatastore_Should_Read_Long_Server_Port()
    {
        ProcessActivity("search", ActivityKind.Client,
            ("db.system", "elasticsearch"), ("db.operation.name", "search"), ("db.elasticsearch.path_parts.index", "index"),
            ("server.address", "localhost"), ("server.port", Port));

        Assert.That(((DatastoreSegmentData)_segmentData).Port, Is.EqualTo(Port));
    }

    [Test]
    public void Messaging_Should_Read_Long_Server_Port()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"), ("server.address", "localhost"), ("server.port", Port));

        Assert.That(((MessageBrokerSegmentData)_segmentData).ServerPort, Is.EqualTo(Port));
    }

    [Test]
    public void Messaging_Should_Map_RabbitMq_Cluster_And_Vhost()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("rabbitmq.cluster.name", "mycluster"), ("rabbitmq.vhost.name", "/"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.EqualTo("mycluster"));
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.EqualTo("/"));
    }

    [Test]
    public void Messaging_Should_Map_Only_Cluster_When_Vhost_Missing()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("rabbitmq.cluster.name", "mycluster"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.EqualTo("mycluster"));
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.Null);
    }

    [Test]
    public void Messaging_Should_Map_Only_Vhost_When_Cluster_Missing()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("rabbitmq.vhost.name", "/"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.Null);
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.EqualTo("/"));
    }

    [Test]
    public void Messaging_Should_Map_Deprecated_RabbitMq_Cluster_And_Vhost()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("messaging.rabbitmq.cluster.name", "mycluster"), ("messaging.rabbitmq.vhost.name", "/"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.EqualTo("mycluster"));
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.EqualTo("/"));
    }

    [Test]
    public void Messaging_Should_Map_Only_Deprecated_Cluster_When_Vhost_Missing()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("messaging.rabbitmq.cluster.name", "mycluster"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.EqualTo("mycluster"));
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.Null);
    }

    [Test]
    public void Messaging_Should_Map_Only_Deprecated_Vhost_When_Cluster_Missing()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("messaging.rabbitmq.vhost.name", "/"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.Null);
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.EqualTo("/"));
    }

    [Test]
    public void Messaging_Should_Prefer_New_RabbitMq_Names_Over_Deprecated_Names()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("messaging.rabbitmq.cluster.name", "oldcluster"), ("messaging.rabbitmq.vhost.name", "/old"),
            ("rabbitmq.cluster.name", "newcluster"), ("rabbitmq.vhost.name", "/new"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.EqualTo("newcluster"));
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.EqualTo("/new"));
    }

    [Test]
    public void Messaging_Should_Leave_RabbitMq_Properties_Null_When_Tags_Missing()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"));

        var messageBrokerSegmentData = (MessageBrokerSegmentData)_segmentData;
        Assert.That(messageBrokerSegmentData.ClusterName, Is.Null);
        Assert.That(messageBrokerSegmentData.VirtualHostName, Is.Null);
    }

    [Test]
    public void Messaging_Should_Not_Add_RabbitMq_Cluster_And_Vhost_As_Custom_Attributes()
    {
        ProcessActivity("publish", ActivityKind.Producer,
            ("messaging.system", "rabbitmq"), ("messaging.destination.name", "queue"),
            ("rabbitmq.cluster.name", "mycluster"), ("rabbitmq.vhost.name", "/"),
            ("messaging.rabbitmq.cluster.name", "oldcluster"), ("messaging.rabbitmq.vhost.name", "/old"));

        Mock.Assert(() => _segment.AddCustomAttribute("rabbitmq.cluster.name", Arg.AnyObject), Occurs.Never());
        Mock.Assert(() => _segment.AddCustomAttribute("rabbitmq.vhost.name", Arg.AnyObject), Occurs.Never());
        Mock.Assert(() => _segment.AddCustomAttribute("messaging.rabbitmq.cluster.name", Arg.AnyObject), Occurs.Never());
        Mock.Assert(() => _segment.AddCustomAttribute("messaging.rabbitmq.vhost.name", Arg.AnyObject), Occurs.Never());
    }

    [Test]
    public void RpcClient_Should_Read_Long_Server_Port()
    {
        ProcessActivity("call", ActivityKind.Client,
            ("rpc.system", "grpc"), ("rpc.service", "Service"), ("rpc.method", "Method"), ("server.address", "localhost"), ("server.port", Port));

        Assert.That(((ExternalGrpcSegmentData)_segmentData).Uri.Port, Is.EqualTo(Port));
    }

    [Test]
    public void RpcServer_Should_Read_Long_Server_Port()
    {
        ProcessActivity("serve", ActivityKind.Server,
            ("rpc.system", "grpc"), ("grpc.method", "Service/Method"), ("server.address", "localhost"), ("server.port", Port));

        Mock.Assert(() => _transaction.SetUri("grpc://localhost:27018/Service/Method"), Occurs.Once());
    }

    private void ProcessActivity(string name, ActivityKind kind, params (string Key, object Value)[] tags)
    {
        using var activity = _activitySource.StartActivity(name, kind);
        foreach (var tag in tags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }

        _segment.ProcessActivityTags(activity, _agent, Mock.Create<IErrorService>());
    }
}

// Regression tests for NR-626857: AWS Lambda invoke cloud.resource_id must use the correct ARN partition.
[TestFixture]
public class ActivityBridgeSegmentHelpersLambdaArnTests
{
    public interface ITestSegment : ISegment, ISegmentExperimental, IHybridAgentSegment { }

    public interface ITestAgent : IAgent, IAgentExperimental { }

    private const string AccountId = "123456789012";

    private ActivitySource _activitySource;
    private ActivityListener _activityListener;
    private ITestSegment _segment;
    private ITestAgent _agent;
    private IConfiguration _configuration;

    [SetUp]
    public void SetUp()
    {
        _activitySource = new ActivitySource(nameof(ActivityBridgeSegmentHelpersLambdaArnTests));
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == _activitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_activityListener);

        _segment = Mock.Create<ITestSegment>();
        Mock.Arrange(() => _segment.SetSegmentData(Arg.IsAny<ISegmentData>())).Returns(_segment);
        Mock.Arrange(() => _segment.AddCloudSdkAttribute(Arg.IsAny<string>(), Arg.IsAny<object>())).Returns(_segment);

        _agent = Mock.Create<ITestAgent>();
        _configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => _configuration.AwsAccountId).Returns(AccountId);
        Mock.Arrange(() => _agent.Configuration).Returns(_configuration);
    }

    [TearDown]
    public void TearDown()
    {
        _activityListener.Dispose();
        _activitySource.Dispose();
    }

    [Test]
    public void CommercialRegion_Should_Build_Standard_Partition_Arn()
    {
        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.us-west-2.amazonaws.com/"), ("http.request.method", "POST"),
            ("faas.invoked_name", "myFunction"), ("faas.invoked_provider", "aws"), ("aws.region", "us-west-2"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id",
            $"arn:aws:lambda:us-west-2:{AccountId}:function:myFunction"), Occurs.Once());
    }

    [Test]
    public void GovCloudRegion_Should_Build_GovCloud_Partition_Arn()
    {
        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.us-gov-west-1.amazonaws.com/"), ("http.request.method", "POST"),
            ("faas.invoked_name", "myFunction"), ("faas.invoked_provider", "aws"), ("aws.region", "us-gov-west-1"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id",
            $"arn:aws-us-gov:lambda:us-gov-west-1:{AccountId}:function:myFunction"), Occurs.Once());
    }

    [Test]
    public void ChinaRegion_Should_Build_China_Partition_Arn()
    {
        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.cn-north-1.amazonaws.com.cn/"), ("http.request.method", "POST"),
            ("faas.invoked_name", "myFunction"), ("faas.invoked_provider", "aws"), ("aws.region", "cn-north-1"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id",
            $"arn:aws-cn:lambda:cn-north-1:{AccountId}:function:myFunction"), Occurs.Once());
    }

    [Test]
    public void FullArnInvokedName_Should_Be_Passed_Through_Unchanged()
    {
        const string fullArn = "arn:aws-us-gov:lambda:us-gov-west-1:123456789012:function:myFunction";
        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.us-gov-west-1.amazonaws.com/"), ("http.request.method", "POST"),
            ("faas.invoked_name", fullArn), ("faas.invoked_provider", "aws"), ("aws.region", "us-gov-west-1"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id", fullArn), Occurs.Once());
    }

    [Test]
    public void NoAwsAccountId_Should_Not_Add_CloudSdkAttribute()
    {
        Mock.Arrange(() => _configuration.AwsAccountId).Returns((string)null);

        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.us-west-2.amazonaws.com/"), ("http.request.method", "POST"),
            ("faas.invoked_name", "myFunction"), ("faas.invoked_provider", "aws"), ("aws.region", "us-west-2"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id", Arg.IsAny<string>()), Occurs.Never());
    }

    [Test]
    public void MissingInvokedName_Should_Not_Add_CloudSdkAttribute()
    {
        ProcessActivity("invoke", ActivityKind.Client,
            ("url.full", "https://lambda.us-west-2.amazonaws.com/"), ("http.request.method", "POST"),
            ("faas.invoked_provider", "aws"), ("aws.region", "us-west-2"));

        Mock.Assert(() => _segment.AddCloudSdkAttribute("cloud.resource_id", Arg.IsAny<string>()), Occurs.Never());
    }

    private void ProcessActivity(string name, ActivityKind kind, params (string Key, object Value)[] tags)
    {
        using var activity = _activitySource.StartActivity(name, kind);
        foreach (var tag in tags)
        {
            activity.SetTag(tag.Key, tag.Value);
        }

        _segment.ProcessActivityTags(activity, _agent, Mock.Create<IErrorService>());
    }
}
