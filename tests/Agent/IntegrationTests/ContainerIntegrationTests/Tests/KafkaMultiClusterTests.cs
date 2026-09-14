// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.ContainerIntegrationTests.Fixtures;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Testing.Assertions;
using Xunit;

namespace NewRelic.Agent.ContainerIntegrationTests.Tests;

// Proves that KafkaClusterIdCache keys cluster ids by bootstrap servers, never
// attaching one cluster's id to another cluster's traffic. Two independent
// single-node KRaft clusters, each with its own CLUSTER_ID and topic.
[Collection("KafkaTests")]
[Trait("Architecture", "amd64")]
[Trait("TestArea", "Messaging")]
public class KafkaMultiClusterTest : NewRelicIntegrationTest<KafkaMultiClusterTestFixture>
{
    private const string ExpectedClusterId1 = "MkU3OEVBNTcwNTJENDM2Qg";
    private const string ExpectedClusterId2 = "fzwakkuOTSGcbypei3HUAw";

    private readonly KafkaMultiClusterTestFixture _fixture;

    public KafkaMultiClusterTest(KafkaMultiClusterTestFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _fixture = fixture;
        _fixture.TestLogger = output;

        _fixture.Actions(setupConfiguration: () =>
            {
                var configModifier = new NewRelicConfigModifier(_fixture.DestinationNewRelicConfigFilePath);
                configModifier.SetLogLevel("finest");
                configModifier.ConfigureFasterMetricsHarvestCycle(10);
                configModifier.ConfigureFasterSpanEventsHarvestCycle(10);

                // The assertions read spans, and a span only exists for a sampled transaction.
                // Adaptive sampling takes 10 transactions per 60 seconds, and cluster 2's id
                // resolves late, so its produce transactions can lose that budget.
                configModifier.SetRootSampler("alwaysOn");

                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC", _fixture.TopicName);
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_TOPIC_2", _fixture.TopicName2);
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_METRICS_CLUSTER_METRICS_ENABLED", "true");
                _fixture.RemoteApplication.SetAdditionalEnvironmentVariable("NEW_RELIC_KAFKA_METRICS_INTERVAL", "10");

                _fixture.RetainAgentLogOnExerciseException = true;
            },
            exerciseApplication: () =>
            {
                _fixture.Delay(10); // wait for kafka and app to be ready
                _fixture.TestLogger.WriteLine("Starting exercise application");
                _fixture.ExerciseApplication();

                _fixture.GetBootstrapServer();

                // Gate on the harvested data the assertions read. Cluster 2 resolves last, so its
                // metric and span imply cluster 1's have already arrived.
                _fixture.TestLogger.WriteLine("Waiting for the cluster 2 produce metric to be harvested");
                _fixture.AgentLog.WaitForMetricAggregateCallCount(
                    $"MessageBroker/Kafka/Cluster/{ExpectedClusterId2}/Produce/{_fixture.TopicName2}",
                    1,
                    TimeSpan.FromMinutes(1));

                _fixture.ShutdownRemoteApplication();
                _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromSeconds(20));
            });

        _fixture.Initialize();
    }

    [Fact]
    public void ClusterIdsAreKeyedByBootstrapServersAndNeverCrossOver()
    {
        var topic1 = _fixture.TopicName;
        var topic2 = _fixture.TopicName2;

        var metrics = _fixture.AgentLog.GetMetrics().ToList();
        var spans = _fixture.AgentLog.GetSpanEvents();

        var produceMetricTopic1 = $"MessageBroker/Kafka/Cluster/{ExpectedClusterId1}/Produce/{topic1}";
        var produceMetricTopic2 = $"MessageBroker/Kafka/Cluster/{ExpectedClusterId2}/Produce/{topic2}";
        var consumeMetricTopic1 = $"MessageBroker/Kafka/Cluster/{ExpectedClusterId1}/Consume/{topic1}";
        var consumeMetricTopic2 = $"MessageBroker/Kafka/Cluster/{ExpectedClusterId2}/Consume/{topic2}";

        var produceMetric1Exists = metrics.Any(m => m.MetricSpec.Name == produceMetricTopic1);
        var produceMetric2Exists = metrics.Any(m => m.MetricSpec.Name == produceMetricTopic2);
        var consumeMetric1Exists = metrics.Any(m => m.MetricSpec.Name == consumeMetricTopic1);
        var consumeMetric2Exists = metrics.Any(m => m.MetricSpec.Name == consumeMetricTopic2);

        // Contamination check: cluster id1 must never appear paired with topic2, and
        // cluster id2 must never appear paired with topic1. Explicit negative assertions
        // over the harvested metric list, not inferred from the positive checks above.
        var id1WithTopic2 = metrics.Where(m =>
            m.MetricSpec.Name.StartsWith($"MessageBroker/Kafka/Cluster/{ExpectedClusterId1}/") &&
            m.MetricSpec.Name.EndsWith($"/{topic2}")).ToList();
        var id2WithTopic1 = metrics.Where(m =>
            m.MetricSpec.Name.StartsWith($"MessageBroker/Kafka/Cluster/{ExpectedClusterId2}/") &&
            m.MetricSpec.Name.EndsWith($"/{topic1}")).ToList();

        var messageBrokerProduceTopic1 = "MessageBroker/Kafka/Topic/Produce/Named/" + topic1;
        var messageBrokerProduceTopic2 = "MessageBroker/Kafka/Topic/Produce/Named/" + topic2;
        // Every produce span for the topic, not the first one. Spans created before the cluster
        // id resolves legitimately carry no id, so the first span is not a valid sample.
        var idsOnTopic1 = spans
            .Where(s => s.IntrinsicAttributes["name"].Equals(messageBrokerProduceTopic1))
            .Select(s => s.UserAttributes.TryGetValue("kafka.cluster.id", out var v) ? v as string : null)
            .ToList();
        var idsOnTopic2 = spans
            .Where(s => s.IntrinsicAttributes["name"].Equals(messageBrokerProduceTopic2))
            .Select(s => s.UserAttributes.TryGetValue("kafka.cluster.id", out var v) ? v as string : null)
            .ToList();

        NrAssert.Multiple(
            () => Assert.True(produceMetric1Exists, $"Expected metric {produceMetricTopic1} to exist"),
            () => Assert.True(produceMetric2Exists, $"Expected metric {produceMetricTopic2} to exist"),
            () => Assert.True(consumeMetric1Exists, $"Expected metric {consumeMetricTopic1} to exist"),
            () => Assert.True(consumeMetric2Exists, $"Expected metric {consumeMetricTopic2} to exist"),

            () => Assert.True(!id1WithTopic2.Any(),
                $"Cluster id1 ({ExpectedClusterId1}) must never be paired with topic2 ({topic2}), but found: {string.Join(", ", id1WithTopic2.Select(m => m.MetricSpec.Name))}"),
            () => Assert.True(!id2WithTopic1.Any(),
                $"Cluster id2 ({ExpectedClusterId2}) must never be paired with topic1 ({topic1}), but found: {string.Join(", ", id2WithTopic1.Select(m => m.MetricSpec.Name))}"),

            () => Assert.True(idsOnTopic1.Any(id => id == ExpectedClusterId1),
                $"Expected at least one of the {idsOnTopic1.Count} produce spans for {topic1} to carry kafka.cluster.id={ExpectedClusterId1}, but found: {DescribeIds(idsOnTopic1)}"),
            () => Assert.True(idsOnTopic2.Any(id => id == ExpectedClusterId2),
                $"Expected at least one of the {idsOnTopic2.Count} produce spans for {topic2} to carry kafka.cluster.id={ExpectedClusterId2}, but found: {DescribeIds(idsOnTopic2)}"),

            // A span must never carry a cluster id belonging to the other cluster. A null id is
            // allowed: those spans were created before that cluster's id resolved.
            () => Assert.True(idsOnTopic1.All(id => id == null || id == ExpectedClusterId1),
                $"Produce spans for {topic1} must only ever carry {ExpectedClusterId1}, but found: {DescribeIds(idsOnTopic1)}"),
            () => Assert.True(idsOnTopic2.All(id => id == null || id == ExpectedClusterId2),
                $"Produce spans for {topic2} must only ever carry {ExpectedClusterId2}, but found: {DescribeIds(idsOnTopic2)}")
        );
    }

    private static string DescribeIds(System.Collections.Generic.IEnumerable<string> ids)
    {
        var described = ids.Select(id => id ?? "<no id>").GroupBy(id => id)
            .Select(g => $"{g.Key} x{g.Count()}");
        return string.Join(", ", described);
    }
}
