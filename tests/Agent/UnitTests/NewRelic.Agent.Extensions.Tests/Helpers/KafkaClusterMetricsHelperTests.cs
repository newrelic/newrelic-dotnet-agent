// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class KafkaClusterMetricsHelperTests
{
    private const string BootstrapServers = "broker1:9092,broker2:9092";

    private IAgent _agent;
    private ISegment _segment;
    private List<string> _recordedMetrics;
    private Dictionary<string, object> _recordedAttributes;
    private int _lookupCallCount;
    private string _capturedBootstrapServers;

    [SetUp]
    public void SetUp()
    {
        _recordedMetrics = new List<string>();
        _recordedAttributes = new Dictionary<string, object>();
        _lookupCallCount = 0;
        _capturedBootstrapServers = null;

        _agent = Mock.Create<IAgent>();
        _segment = Mock.Create<ISegment>();

        Mock.Arrange(() => _agent.RecordCountMetric(Arg.IsAny<string>(), Arg.IsAny<long>()))
            .DoInstead((string metricName, long count) => _recordedMetrics.Add(metricName));

        Mock.Arrange(() => _segment.AddCustomAttribute(Arg.IsAny<string>(), Arg.IsAny<object>()))
            .DoInstead((string key, object value) => _recordedAttributes[key] = value);
    }

    [Test]
    public void RecordClusterMetrics_RecordsTheProduceMetric_AndTheClusterIdAttribute()
    {
        ArrangeClusterMetricsEnabled(true);

        KafkaClusterMetricsHelper.RecordClusterMetrics(_agent, _segment, BootstrapServers, "mytopic", KafkaClusterOperation.Produce, ResolvedLookup("cluster-abc"));

        Assert.Multiple(() =>
        {
            Assert.That(_recordedMetrics, Is.EqualTo(new[] { "MessageBroker/Kafka/Cluster/cluster-abc/Produce/mytopic" }));
            Assert.That(_recordedAttributes, Does.ContainKey("kafka.cluster.id"));
            Assert.That(_recordedAttributes["kafka.cluster.id"], Is.EqualTo("cluster-abc"));
            Assert.That(_capturedBootstrapServers, Is.EqualTo(BootstrapServers));
        });
    }

    [Test]
    public void RecordClusterMetrics_RecordsTheConsumeMetric_AndTheClusterIdAttribute()
    {
        ArrangeClusterMetricsEnabled(true);

        KafkaClusterMetricsHelper.RecordClusterMetrics(_agent, _segment, BootstrapServers, "mytopic", KafkaClusterOperation.Consume, ResolvedLookup("cluster-abc"));

        Assert.Multiple(() =>
        {
            Assert.That(_recordedMetrics, Is.EqualTo(new[] { "MessageBroker/Kafka/Cluster/cluster-abc/Consume/mytopic" }));
            Assert.That(_recordedAttributes["kafka.cluster.id"], Is.EqualTo("cluster-abc"));
        });
    }

    [Test]
    public void RecordClusterMetrics_RecordsNothing_WhenClusterMetricsAreDisabled()
    {
        ArrangeClusterMetricsEnabled(false);

        KafkaClusterMetricsHelper.RecordClusterMetrics(_agent, _segment, BootstrapServers, "mytopic", KafkaClusterOperation.Produce, ResolvedLookup("cluster-abc"));

        Assert.Multiple(() =>
        {
            Assert.That(_recordedMetrics, Is.Empty);
            Assert.That(_recordedAttributes, Is.Empty);
            Assert.That(_lookupCallCount, Is.EqualTo(0), "the cluster id lookup must not run when the config gate is off");
        });
    }

    [Test]
    public void RecordClusterMetrics_RecordsNothing_WhenNoClusterIdIsResolved()
    {
        ArrangeClusterMetricsEnabled(true);

        KafkaClusterMetricsHelper.RecordClusterMetrics(_agent, _segment, BootstrapServers, "mytopic", KafkaClusterOperation.Produce, UnresolvedLookup());

        Assert.Multiple(() =>
        {
            Assert.That(_recordedMetrics, Is.Empty);
            Assert.That(_recordedAttributes, Is.Empty);
            Assert.That(_lookupCallCount, Is.EqualTo(1));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void RecordClusterMetrics_RecordsNothing_WhenTheLookupSucceedsWithAnEmptyClusterId(string clusterId)
    {
        ArrangeClusterMetricsEnabled(true);

        KafkaClusterMetricsHelper.RecordClusterMetrics(_agent, _segment, BootstrapServers, "mytopic", KafkaClusterOperation.Produce, ResolvedLookup(clusterId));

        Assert.Multiple(() =>
        {
            Assert.That(_recordedMetrics, Is.Empty);
            Assert.That(_recordedAttributes, Is.Empty);
        });
    }

    private void ArrangeClusterMetricsEnabled(bool enabled)
    {
        var configuration = Mock.Create<IConfiguration>();
        Mock.Arrange(() => configuration.KafkaClusterMetricsEnabled).Returns(enabled);
        Mock.Arrange(() => _agent.Configuration).Returns(configuration);
    }

    private ClusterIdLookup ResolvedLookup(string clusterId)
    {
        return (string bootstrapServers, out string resolved) =>
        {
            _lookupCallCount++;
            _capturedBootstrapServers = bootstrapServers;
            resolved = clusterId;
            return true;
        };
    }

    private ClusterIdLookup UnresolvedLookup()
    {
        return (string bootstrapServers, out string resolved) =>
        {
            _lookupCallCount++;
            _capturedBootstrapServers = bootstrapServers;
            resolved = null;
            return false;
        };
    }
}
