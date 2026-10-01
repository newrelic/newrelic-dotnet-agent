// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
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
