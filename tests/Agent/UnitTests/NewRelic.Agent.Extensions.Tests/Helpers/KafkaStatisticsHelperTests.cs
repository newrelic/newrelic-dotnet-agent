// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class KafkaStatisticsHelperTests
{
    #region Test Data

    private const string ValidProducerStatisticsJson = @"{
        ""name"": ""rdkafka"",
        ""client_id"": ""producer-test"",
        ""type"": ""producer"",
        ""ts"": 5016483227792,
        ""tx"": 15,
        ""rx"": 12,
        ""txmsgs"": 100,
        ""rxmsgs"": 50,
        ""txmsg_bytes"": 5000,
        ""rxmsg_bytes"": 2500,
        ""metadata_cache_cnt"": 3,
        ""msg_cnt"": 10,
        ""msg_size"": 250,
        ""tx_bytes"": 7500,
        ""rx_bytes"": 3750,
        ""brokers"": {
            ""1"": {
                ""name"": ""broker-1"",
                ""nodeid"": 1,
                ""source"": ""learned"",
                ""state"": ""UP"",
                ""tx"": 10,
                ""rx"": 8,
                ""txbytes"": 4000,
                ""rxbytes"": 2000,
                ""txerrs"": 1,
                ""rxerrs"": 0,
                ""connects"": 1,
                ""disconnects"": 0,
                ""rtt"": {
                    ""min"": 5,
                    ""max"": 15,
                    ""avg"": 10,
                    ""sum"": 100,
                    ""cnt"": 10
                },
                ""req"": {
                    ""Produce"": 481,
                    ""Metadata"": 2,
                    ""ApiVersion"": 1
                }
            }
        },
        ""topics"": {
            ""test-topic"": {
                ""topic"": ""test-topic"",
                ""metadata_age"": 1000,
                ""batchsize"": {
                    ""min"": 100,
                    ""max"": 500,
                    ""avg"": 300,
                    ""sum"": 3000,
                    ""cnt"": 10
                },
                ""batchcnt"": {
                    ""min"": 1,
                    ""max"": 5,
                    ""avg"": 3,
                    ""sum"": 30,
                    ""cnt"": 10
                },
                ""partitions"": {
                    ""0"": {
                        ""partition"": 0,
                        ""broker"": 1,
                        ""leader"": 1,
                        ""txmsgs"": 50,
                        ""txbytes"": 2500,
                        ""rxmsgs"": 25,
                        ""rxbytes"": 1250,
                        ""consumer_lag"": 5,
                        ""lo_offset"": 0,
                        ""hi_offset"": 100,
                        ""committed_offset"": 95
                    }
                }
            }
        },
        ""eos"": {
            ""idemp_state"": ""Init"",
            ""producer_id"": 12345,
            ""producer_epoch"": 1,
            ""epoch_cnt"": 0
        }
    }";

    private const string ValidConsumerStatisticsJson = @"{
        ""name"": ""rdkafka"",
        ""client_id"": ""consumer-test"",
        ""type"": ""consumer"",
        ""ts"": 8500000000000,
        ""tx"": 8,
        ""rx"": 20,
        ""txmsgs"": 25,
        ""rxmsgs"": 150,
        ""txmsg_bytes"": 1250,
        ""rxmsg_bytes"": 7500,
        ""metadata_cache_cnt"": 2,
        ""msg_cnt"": 5,
        ""msg_size"": 150,
        ""tx_bytes"": 2000,
        ""rx_bytes"": 10000,
        ""brokers"": {
            ""1"": {
                ""name"": ""broker-1"",
                ""nodeid"": 1,
                ""source"": ""learned"",
                ""state"": ""UP"",
                ""tx"": 5,
                ""rx"": 15,
                ""txbytes"": 1000,
                ""rxbytes"": 5000,
                ""txerrs"": 0,
                ""rxerrs"": 1,
                ""connects"": 1,
                ""disconnects"": 0,
                ""req"": {
                    ""Fetch"": 1216,
                    ""Metadata"": 1,
                    ""Heartbeat"": 0,
                    ""Unknown-62?"": 0
                }
            },
            ""GroupCoordinator"": {
                ""name"": ""GroupCoordinator"",
                ""nodeid"": -1,
                ""source"": ""logical"",
                ""state"": ""UP"",
                ""tx"": 67,
                ""rx"": 66,
                ""txbytes"": 7090,
                ""rxbytes"": 2300,
                ""txerrs"": 0,
                ""rxerrs"": 0,
                ""connects"": 1,
                ""disconnects"": 0,
                ""req"": {
                    ""Heartbeat"": 36,
                    ""OffsetCommit"": 24,
                    ""JoinGroup"": 2,
                    ""SyncGroup"": 1,
                    ""Metadata"": 2,
                    ""Fetch"": 0
                }
            }
        },
        ""topics"": {
            ""test-topic"": {
                ""topic"": ""test-topic"",
                ""metadata_age"": 2000,
                ""partitions"": {
                    ""0"": {
                        ""partition"": 0,
                        ""broker"": 1,
                        ""leader"": 1,
                        ""txmsgs"": 10,
                        ""txbytes"": 500,
                        ""rxmsgs"": 75,
                        ""rxbytes"": 3750,
                        ""consumer_lag"": 10,
                        ""lo_offset"": 0,
                        ""hi_offset"": 200,
                        ""committed_offset"": 190
                    },
                    ""1"": {
                        ""partition"": 1,
                        ""broker"": 1,
                        ""leader"": 1,
                        ""txmsgs"": 15,
                        ""txbytes"": 750,
                        ""rxmsgs"": 75,
                        ""rxbytes"": 3750,
                        ""consumer_lag"": 15,
                        ""lo_offset"": 0,
                        ""hi_offset"": 150,
                        ""committed_offset"": 135
                    }
                }
            }
        },
        ""cgrp"": {
            ""state"": ""up"",
            ""rebalance_cnt"": 2,
            ""rebalance_age"": 30000,
            ""assignment_size"": 2,
            ""rebalance_reason"": ""join failed""
        }
    }";

    private const string MinimalValidJson = @"{
        ""name"": ""rdkafka"",
        ""client_id"": ""minimal"",
        ""type"": ""producer"",
        ""ts"": 1000000000,
        ""tx"": 1,
        ""rx"": 1,
        ""txmsgs"": 1,
        ""rxmsgs"": 1,
        ""txmsg_bytes"": 100,
        ""rxmsg_bytes"": 100,
        ""metadata_cache_cnt"": 1
    }";

    #endregion

    #region ParseStatistics Tests

    [Test]
    public void ParseStatistics_ValidProducerJson_ReturnsCorrectData()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.IsValid(result), Is.True);
        Assert.That(KafkaStatisticsHelper.GetClientId(result), Is.EqualTo("producer-test"));
        Assert.That(result.Type, Is.EqualTo("producer"));
        Assert.That(result.Tx, Is.EqualTo(15));
        Assert.That(result.Rx, Is.EqualTo(12));
        Assert.That(result.TxMsgs, Is.EqualTo(100));
        Assert.That(result.RxMsgs, Is.EqualTo(50));
        Assert.That(result.TxMsgBytes, Is.EqualTo(5000));
        Assert.That(result.RxMsgBytes, Is.EqualTo(2500));
        Assert.That(result.MetadataCacheCnt, Is.EqualTo(3));
        Assert.That(result.MsgCnt, Is.EqualTo(10));
        Assert.That(result.MsgSize, Is.EqualTo(250));
        Assert.That(result.TxBytes, Is.EqualTo(7500));
        Assert.That(result.RxBytes, Is.EqualTo(3750));
    }

    [Test]
    public void ParseStatistics_ValidConsumerJson_ReturnsCorrectData()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.IsValid(result), Is.True);
        Assert.That(KafkaStatisticsHelper.GetClientId(result), Is.EqualTo("consumer-test"));
        Assert.That(result.Type, Is.EqualTo("consumer"));
        Assert.That(result.Tx, Is.EqualTo(8));
        Assert.That(result.Rx, Is.EqualTo(20));
        Assert.That(result.TxMsgs, Is.EqualTo(25));
        Assert.That(result.RxMsgs, Is.EqualTo(150));
    }

    [Test]
    public void ParseStatistics_ValidProducerJson_ParsesProducerEosMetrics()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

        Assert.That(result.ProducerEos, Is.Not.Null);
        Assert.That(result.ProducerEos.IdempotentState, Is.EqualTo("Init"));
        Assert.That(result.ProducerEos.ProducerId, Is.EqualTo(12345));
        Assert.That(result.ProducerEos.ProducerEpoch, Is.EqualTo(1));
        Assert.That(result.ProducerEos.EpochCount, Is.EqualTo(0));
    }

    [Test]
    public void ParseStatistics_ValidProducerJson_ParsesBatchMetricsViaDictionary()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(result);

        // BatchSizeAvg is computed from topics and reported as a metric
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg"].Value, Is.EqualTo(300));
    }

    [Test]
    public void ParseStatistics_ValidConsumerJson_ParsesConsumerGroupMetrics()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

        Assert.That(result.ConsumerGroup, Is.Not.Null);
        Assert.That(result.ConsumerGroup.State, Is.EqualTo("up"));
        Assert.That(result.ConsumerGroup.RebalanceCount, Is.EqualTo(2));
        Assert.That(result.ConsumerGroup.RebalanceAge, Is.EqualTo(30000));
        Assert.That(result.ConsumerGroup.AssignmentSize, Is.EqualTo(2));
        Assert.That(result.ConsumerGroup.RebalanceReason, Is.EqualTo("join failed"));
    }

    [Test]
    public void ParseStatistics_ConsumerJsonWithMultiplePartitions_TotalConsumerLagInMetrics()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(result);

        // Total consumer lag (10 + 15 = 25) is reported as records-lag-max
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"].Value, Is.EqualTo(25));

        // Per-partition lag
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"].Value, Is.EqualTo(15));
    }

    [Test]
    public void ParseStatistics_ValidJson_ParsesBrokerMetrics()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

        Assert.That(result.Brokers, Has.Count.EqualTo(1));
        var broker = result.Brokers["1"];
        Assert.That(broker.Name, Is.EqualTo("broker-1"));
        Assert.That(broker.NodeId, Is.EqualTo(1));
        Assert.That(broker.State, Is.EqualTo("UP"));
        Assert.That(broker.Tx, Is.EqualTo(10));
        Assert.That(broker.Rx, Is.EqualTo(8));
        Assert.That(broker.TxBytes, Is.EqualTo(4000));
        Assert.That(broker.RxBytes, Is.EqualTo(2000));
        Assert.That(broker.TxErrs, Is.EqualTo(1));
        Assert.That(broker.RxErrs, Is.EqualTo(0));
        Assert.That(broker.Connects, Is.EqualTo(1));
        Assert.That(broker.Disconnects, Is.EqualTo(0));
        Assert.That(broker.RoundTripTime.Avg, Is.EqualTo(10));
    }

    [Test]
    public void ParseStatistics_ValidJson_ParsesTopicAndPartitionStats()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

        Assert.That(result.Topics, Has.Count.EqualTo(1));
        var topic = result.Topics["test-topic"];
        Assert.That(topic.Topic, Is.EqualTo("test-topic"));
        Assert.That(topic.MetadataAge, Is.EqualTo(1000));
        Assert.That(topic.BatchSize.Avg, Is.EqualTo(300));
        Assert.That(topic.BatchCount.Avg, Is.EqualTo(3));
        Assert.That(topic.Partitions, Has.Count.EqualTo(1));

        var partition = topic.Partitions["0"];
        Assert.That(partition.Partition, Is.EqualTo(0));
        Assert.That(partition.Broker, Is.EqualTo(1));
        Assert.That(partition.Leader, Is.EqualTo(1));
        Assert.That(partition.TxMsgs, Is.EqualTo(50));
        Assert.That(partition.TxBytes, Is.EqualTo(2500));
        Assert.That(partition.RxMsgs, Is.EqualTo(25));
        Assert.That(partition.RxBytes, Is.EqualTo(1250));
        Assert.That(partition.ConsumerLag, Is.EqualTo(5));
        Assert.That(partition.LowWatermark, Is.EqualTo(0));
        Assert.That(partition.HighWatermark, Is.EqualTo(100));
        Assert.That(partition.CommittedOffset, Is.EqualTo(95));
    }

    [Test]
    public void ParseStatistics_NullJson_ReturnsNull()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(null);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseStatistics_EmptyJson_ReturnsNull()
    {
        var result = KafkaStatisticsHelper.ParseStatistics("");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseStatistics_InvalidJson_ReturnsNull()
    {
        var result = KafkaStatisticsHelper.ParseStatistics("invalid json");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseStatistics_JsonWithNullResult_ReturnsNull()
    {
        var result = KafkaStatisticsHelper.ParseStatistics("null");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ParseStatistics_MinimalValidJson_ReturnsValidData()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.IsValid(result), Is.True);
        Assert.That(KafkaStatisticsHelper.GetClientId(result), Is.EqualTo("minimal"));
        Assert.That(result.Type, Is.EqualTo("producer"));
    }

    [Test]
    public void ParseStatistics_JsonWithoutClientId_FallsBackToName()
    {
        var jsonWithoutClientId = @"{
            ""name"": ""fallback-name"",
            ""type"": ""producer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1
        }";

        var result = KafkaStatisticsHelper.ParseStatistics(jsonWithoutClientId);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.GetClientId(result), Is.EqualTo("fallback-name"));
    }

    [Test]
    public void ParseStatistics_JsonMissingRequiredFields_ReturnsInvalidData()
    {
        var incompleteJson = @"{
            ""name"": ""incomplete"",
            ""tx"": 1
        }";

        var result = KafkaStatisticsHelper.ParseStatistics(incompleteJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.IsValid(result), Is.False); // Missing type
    }

    #endregion

    #region IsValid Tests

    [Test]
    public void IsValid_ValidData_ReturnsTrue()
    {
        var stats = new KafkaStatisticsHelper.KafkaStatistics
        {
            ClientId = "test-client",
            Type = "producer"
        };

        Assert.That(KafkaStatisticsHelper.IsValid(stats), Is.True);
    }

    [Test]
    public void IsValid_MissingClientId_FallsBackToName()
    {
        var stats = new KafkaStatisticsHelper.KafkaStatistics
        {
            Name = "fallback",
            Type = "producer"
        };

        Assert.That(KafkaStatisticsHelper.IsValid(stats), Is.True);
    }

    [Test]
    public void IsValid_NullClientIdAndName_ReturnsFalse()
    {
        var stats = new KafkaStatisticsHelper.KafkaStatistics
        {
            ClientId = null,
            Name = null,
            Type = "producer"
        };

        Assert.That(KafkaStatisticsHelper.IsValid(stats), Is.False);
    }

    [Test]
    public void IsValid_EmptyClientIdAndName_ReturnsFalse()
    {
        var stats = new KafkaStatisticsHelper.KafkaStatistics
        {
            ClientId = "",
            Name = "",
            Type = "producer"
        };

        Assert.That(KafkaStatisticsHelper.IsValid(stats), Is.False);
    }

    [Test]
    public void IsValid_MissingType_ReturnsFalse()
    {
        var stats = new KafkaStatisticsHelper.KafkaStatistics
        {
            ClientId = "test-client",
            Type = null
        };

        Assert.That(KafkaStatisticsHelper.IsValid(stats), Is.False);
    }

    [Test]
    public void IsValid_Null_ReturnsFalse()
    {
        Assert.That(KafkaStatisticsHelper.IsValid(null), Is.False);
    }

    #endregion

    #region CreateMetricsDictionary Tests

    [Test]
    public void CreateMetricsDictionary_ValidProducerData_CreatesCorrectMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Not.Empty);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/outgoing-byte-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-send-total");

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"].Value, Is.EqualTo(15));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"].Value, Is.EqualTo(12));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"].Value, Is.EqualTo(100));
    }

    [Test]
    public void CreateMetricsDictionary_ValidConsumerData_CreatesCorrectMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Not.Empty);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max");

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"].Value, Is.EqualTo(2));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].Value, Is.EqualTo(2));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"].Value, Is.EqualTo(150));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesBrokerMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/response-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/connection-count");

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"].Value, Is.EqualTo(4000));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesTopicMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesPartitionMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total");

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"].Value, Is.EqualTo(2500));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerData_CreatesPartitionLagMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag");

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"].Value, Is.EqualTo(15));
    }

    [Test]
    public void CreateMetricsDictionary_CumulativeCountersIncludedAtZero_GaugesFilteredAtZero()
    {
        // Cumulative counters are always recorded so the drain's delta machinery has a
        // continuous baseline (even at zero). Gauges at zero are not interesting and are filtered.
        var jsonWithZeros = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""test-zeros"",
            ""type"": ""producer"",
            ""tx"": 5,
            ""rx"": 0,
            ""txmsgs"": 10,
            ""rxmsgs"": 0,
            ""txmsg_bytes"": 0,
            ""rxmsg_bytes"": 0,
            ""metadata_cache_cnt"": 0
        }";
        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithZeros);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // Cumulative counters — included even when value is 0
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/response-counter");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter"].Value, Is.EqualTo(5));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/response-counter"].Value, Is.EqualTo(0));
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/rxmsgs");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/rxmsgs"].Value, Is.EqualTo(0));

        // Gauge at zero — filtered out
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/metadata_cache_cnt");
    }

    [Test]
    public void CreateMetricsDictionary_NullStats_ReturnsEmptyDictionary()
    {
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(null);

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Empty);
    }

    [Test]
    public void CreateMetricsDictionary_InvalidStats_ReturnsEmptyDictionary()
    {
        var invalidStats = new KafkaStatisticsHelper.KafkaStatistics
        {
            ClientId = null,
            Type = "producer"
        };

        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(invalidStats);

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Empty);
    }

    [Test]
    public void CreateMetricsDictionary_ProducerWithoutBatchMetrics_HandlesGracefully()
    {
        var producerWithoutBatchesJson = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""producer-no-batch"",
            ""type"": ""producer"",
            ""tx"": 5,
            ""rx"": 3,
            ""txmsgs"": 10,
            ""rxmsgs"": 5,
            ""txmsg_bytes"": 500,
            ""rxmsg_bytes"": 250,
            ""metadata_cache_cnt"": 1,
            ""topics"": {
                ""test-topic"": {
                    ""topic"": ""test-topic"",
                    ""metadata_age"": 1000
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(producerWithoutBatchesJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(stats, Is.Not.Null);
        // No batch-size-avg metric when batch metrics are absent
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-no-batch/batch-size-avg");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-no-batch/request-counter");
    }

    [Test]
    public void ParseStatistics_JsonWithMissingOptionalFields_ReturnsValidData()
    {
        var minimalJson = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""minimal"",
            ""type"": ""producer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1
        }";

        var result = KafkaStatisticsHelper.ParseStatistics(minimalJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(KafkaStatisticsHelper.IsValid(result), Is.True);
        Assert.That(result.Brokers, Is.Empty);
        Assert.That(result.Topics, Is.Empty);
    }

    #endregion

    #region PopulateMetricsDictionary Tests

    [Test]
    public void PopulateMetricsDictionary_ReusesDictionary()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = new System.Collections.Generic.Dictionary<string, KafkaMetricValue>();

        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats);
        var firstCount = metrics.Count;
        Assert.That(firstCount, Is.GreaterThan(0));

        // Populate again — should clear and refill
        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats);
        Assert.That(metrics.Count, Is.EqualTo(firstCount));
    }

    [Test]
    public void PopulateMetricsDictionary_ClearsPreviousEntries()
    {
        var metrics = new System.Collections.Generic.Dictionary<string, KafkaMetricValue>();
        metrics["stale/metric"] = new KafkaMetricValue(999, KafkaMetricType.Gauge);

        var stats = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);
        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats);

        MetricAssertions.ExpectNoKey(metrics, "stale/metric");
    }

    [Test]
    public void PopulateMetricsDictionary_NullStats_ClearsDictionary()
    {
        var metrics = new System.Collections.Generic.Dictionary<string, KafkaMetricValue>();
        metrics["existing"] = new KafkaMetricValue(1, KafkaMetricType.Gauge);

        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, null);

        Assert.That(metrics, Is.Empty);
    }

    #endregion

    #region Metric Type Classification Tests

    [Test]
    public void CreateMetricsDictionary_CumulativeCounters_AreTaggedAsCumulative()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // Client-level cumulative counters
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/outgoing-byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));

        // Broker-level cumulative counters
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));

        // Partition-level cumulative counters
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
    }

    [Test]
    public void CreateMetricsDictionary_GaugeValues_AreTaggedAsGauge()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/metadata_cache_cnt"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-queue-time-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerGaugeValues_AreTaggedAsGauge()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
    }

    [Test]
    public void CreateMetricsDictionary_WindowAverages_AreTaggedAsWindowAvg()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerCumulativeCounters_AreTaggedAsCumulative()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/bytes-consumed-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
    }

    #endregion

    #region NoDuplicateMetrics Tests

    [Test]
    public void CreateMetricsDictionary_NoDuplicateRequestCounterAndRequestTotal()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // Client-level should have request-counter but NOT request-total (was a duplicate)
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter");
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter");
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-total");

        // Node-level should still have request-total (not a duplicate — different scope)
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total");
    }

    #endregion

    #region NormalizeNodeId Tests (via broker metrics)

    [Test]
    public void CreateMetricsDictionary_NegativeNodeId_NormalizesToSeed()
    {
        var jsonWithNegativeNodeId = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""test-client"",
            ""type"": ""producer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1,
            ""brokers"": {
                ""bootstrap"": {
                    ""name"": ""bootstrap"",
                    ""nodeid"": -1,
                    ""state"": ""UP"",
                    ""tx"": 5,
                    ""rx"": 3,
                    ""txbytes"": 1000,
                    ""rxbytes"": 500,
                    ""txerrs"": 0,
                    ""rxerrs"": 0,
                    ""connects"": 1,
                    ""disconnects"": 0
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithNegativeNodeId);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/seed/client/test-client/request-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/seed/client/test-client/request-total"].Value, Is.EqualTo(5));
    }

    [Test]
    public void CreateMetricsDictionary_CoordinatorNodeId_NormalizesToCoordinator()
    {
        // Coordinator node IDs are int.MaxValue - coordinatorId, where coordinatorId is in (0, 1000)
        var coordinatorNodeId = int.MaxValue - 5; // coordinator-5
        var jsonWithCoordinatorNodeId = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""test-client"",
            ""type"": ""consumer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1,
            ""brokers"": {
                ""coordinator"": {
                    ""name"": ""coordinator"",
                    ""nodeid"": " + coordinatorNodeId + @",
                    ""state"": ""UP"",
                    ""tx"": 7,
                    ""rx"": 4,
                    ""txbytes"": 2000,
                    ""rxbytes"": 1000,
                    ""txerrs"": 0,
                    ""rxerrs"": 0,
                    ""connects"": 2,
                    ""disconnects"": 0
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithCoordinatorNodeId);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator-5/client/test-client/request-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator-5/client/test-client/request-total"].Value, Is.EqualTo(7));
    }

    [Test]
    public void CreateMetricsDictionary_MaxValueNodeId_UsesRawNodeId()
    {
        // nodeId == int.MaxValue → coordinatorId = 0, fails coordinatorId > 0 check
        var maxNodeId = int.MaxValue;
        var jsonWithMaxNodeId = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""test-client"",
            ""type"": ""producer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1,
            ""brokers"": {
                ""maxnode"": {
                    ""name"": ""max-node"",
                    ""nodeid"": " + maxNodeId + @",
                    ""state"": ""UP"",
                    ""tx"": 2,
                    ""rx"": 1,
                    ""txbytes"": 300,
                    ""rxbytes"": 150,
                    ""txerrs"": 0,
                    ""rxerrs"": 0,
                    ""connects"": 1,
                    ""disconnects"": 0
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithMaxNodeId);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/" + maxNodeId + "/client/test-client/request-total");
    }

    [Test]
    public void CreateMetricsDictionary_LargeNodeIdNotCoordinator_UsesRawNodeId()
    {
        // A large node ID that doesn't map to a valid coordinator (coordinatorId >= 1000)
        var largeNodeId = int.MaxValue - 5000; // coordinatorId = 5000, not in (0, 1000)
        var jsonWithLargeNodeId = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""test-client"",
            ""type"": ""producer"",
            ""tx"": 1,
            ""rx"": 1,
            ""txmsgs"": 1,
            ""rxmsgs"": 1,
            ""txmsg_bytes"": 100,
            ""rxmsg_bytes"": 100,
            ""metadata_cache_cnt"": 1,
            ""brokers"": {
                ""large"": {
                    ""name"": ""large-node"",
                    ""nodeid"": " + largeNodeId + @",
                    ""state"": ""UP"",
                    ""tx"": 3,
                    ""rx"": 2,
                    ""txbytes"": 500,
                    ""rxbytes"": 250,
                    ""txerrs"": 0,
                    ""rxerrs"": 0,
                    ""connects"": 1,
                    ""disconnects"": 0
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithLargeNodeId);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/" + largeNodeId + "/client/test-client/request-total");
    }

    #endregion

    #region Ts Field Parsing Tests

    [Test]
    public void ParseStatistics_ValidProducerJson_ParsesTsField()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

        Assert.That(result.Ts, Is.EqualTo(5016483227792L));
    }

    [Test]
    public void ParseStatistics_ValidConsumerJson_ParsesTsField()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

        Assert.That(result.Ts, Is.EqualTo(8500000000000L));
    }

    [Test]
    public void ParseStatistics_JsonWithoutTsField_TsIsZero()
    {
        var jsonWithoutTs = @"{
            ""name"": ""rdkafka"",
            ""client_id"": ""no-ts"",
            ""type"": ""producer"",
            ""tx"": 5,
            ""rx"": 3,
            ""txmsgs"": 10,
            ""rxmsgs"": 5,
            ""txmsg_bytes"": 500,
            ""rxmsg_bytes"": 250,
            ""metadata_cache_cnt"": 1
        }";

        var result = KafkaStatisticsHelper.ParseStatistics(jsonWithoutTs);

        Assert.That(result.Ts, Is.EqualTo(0L));
    }

    [Test]
    public void ParseStatistics_TsFieldIsNotAffectedByPopulateMetricsDictionary()
    {
        // Ts is a model field, not emitted as a metric — verify it is not added to the metrics dictionary
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics.Keys, Has.None.Contains("/ts"));
        Assert.That(metrics.Keys, Has.None.Contains("ts"));
    }

    #endregion

    #region Broker req / source Field Tests (Kafka protocol request-type counters)

    [Test]
    public void ParseStatistics_ConsumerJson_ParsesBrokerSource()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

        Assert.That(result.Brokers["1"].Source, Is.EqualTo("learned"));
        Assert.That(result.Brokers["GroupCoordinator"].Source, Is.EqualTo("logical"));
    }

    [Test]
    public void ParseStatistics_ConsumerJson_ParsesRequestCounts()
    {
        var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

        Assert.That(result.Brokers["1"].RequestCounts["Fetch"], Is.EqualTo(1216));
        Assert.That(result.Brokers["GroupCoordinator"].RequestCounts["Heartbeat"], Is.EqualTo(36));
        Assert.That(result.Brokers["GroupCoordinator"].RequestCounts["OffsetCommit"], Is.EqualTo(24));
        Assert.That(result.Brokers["GroupCoordinator"].RequestCounts["JoinGroup"], Is.EqualTo(2));
        Assert.That(result.Brokers["GroupCoordinator"].RequestCounts["SyncGroup"], Is.EqualTo(1));
    }

    [Test]
    public void CreateMetricsDictionary_GroupCoordinatorBroker_NormalizesToCoordinator()
    {
        // The GroupCoordinator logical broker has nodeid=-1 but source=logical — our code must
        // NOT conflate it with seed brokers (which also have nodeid=-1).
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/heartbeat-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/heartbeat-total"].Value, Is.EqualTo(36));
    }

    [Test]
    public void CreateMetricsDictionary_SeedBrokerWithoutLogicalSource_StillNormalizesToSeed()
    {
        // Regression guard: without source="logical", a nodeid=-1 broker must still be "seed".
        var jsonWithSeedBroker = @"{
            ""name"": ""rdkafka"", ""client_id"": ""seed-test"", ""type"": ""producer"",
            ""tx"": 1, ""rx"": 1, ""txmsgs"": 1, ""rxmsgs"": 1,
            ""txmsg_bytes"": 100, ""rxmsg_bytes"": 100, ""metadata_cache_cnt"": 1,
            ""brokers"": {
                ""bootstrap"": {
                    ""name"": ""bootstrap"", ""nodeid"": -1, ""source"": ""configured"",
                    ""state"": ""UP"", ""tx"": 5, ""rx"": 3, ""txbytes"": 1000, ""rxbytes"": 500,
                    ""txerrs"": 0, ""rxerrs"": 0, ""connects"": 1, ""disconnects"": 0
                }
            }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithSeedBroker);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/seed/client/seed-test/request-total");
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/producer-node-metrics/node/coordinator/client/seed-test/request-total");
    }

    [Test]
    public void CreateMetricsDictionary_PerBrokerReqMetrics_EmittedWithTotalSuffix()
    {
        // Cumulative counters are recorded at all values (including 0) so the drain maintains
        // a continuous baseline for rate derivation. Zero-valued counters are still filtered
        // at wire-emission time in the drain (via the valueToReport > 0 gate).
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // Data broker: fetch=1216, heartbeat=0 — both present; heartbeat's value is 0.
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/1/client/consumer-test/fetch-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/1/client/consumer-test/fetch-total"].Value, Is.EqualTo(1216));
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/1/client/consumer-test/heartbeat-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/1/client/consumer-test/heartbeat-total"].Value, Is.EqualTo(0));

        // Coordinator broker: heartbeat=36, commit=24, fetch=0 — all present.
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/heartbeat-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/commit-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/commit-total"].Value, Is.EqualTo(24));
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/fetch-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/fetch-total"].Value, Is.EqualTo(0));
    }

    [Test]
    public void CreateMetricsDictionary_UnknownReqKeys_AreFiltered()
    {
        // The "Unknown-62?" entry in the fixture must not produce any metric — the allowlist filters it.
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics.Keys, Has.None.Contains("Unknown"));
        Assert.That(metrics.Keys, Has.None.Contains("unknown"));
    }

    [Test]
    public void CreateMetricsDictionary_ReqMetrics_TaggedAsCumulative()
    {
        // Rate metrics are only produced from Cumulative-tagged metrics.
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/coordinator/client/consumer-test/heartbeat-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-node-metrics/node/1/client/consumer-test/fetch-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerClientLevelAggregates_EmitUnderJavaGroupPaths()
    {
        // Java parity: client-level aggregated metrics under consumer-coordinator-metrics/ and
        // consumer-fetch-manager-metrics/. These sum across all brokers.
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // Heartbeat is 36 on coordinator + 0 on data broker = 36
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/heartbeat-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/heartbeat-total"].Value, Is.EqualTo(36));

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/commit-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/commit-total"].Value, Is.EqualTo(24));

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/join-total");
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/sync-total");

        // Fetch aggregate under fetch-manager path
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/fetch-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/fetch-total"].Value, Is.EqualTo(1216));
    }

    [Test]
    public void CreateMetricsDictionary_ProducerClientLevelAggregates_EmitUnderProducerMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/produce-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/produce-total"].Value, Is.EqualTo(481));

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/metadata-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/metadata-total"].Value, Is.EqualTo(2));

        // Consumer-scoped aggregates must not leak into producer metrics
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/producer-test/heartbeat-total");
    }

    [Test]
    public void CreateMetricsDictionary_NoBrokersReqData_DoesNotEmitAggregates()
    {
        var jsonNoReq = @"{
            ""name"": ""rdkafka"", ""client_id"": ""no-req"", ""type"": ""consumer"",
            ""tx"": 1, ""rx"": 1, ""txmsgs"": 1, ""rxmsgs"": 1,
            ""txmsg_bytes"": 100, ""rxmsg_bytes"": 100, ""metadata_cache_cnt"": 1,
            ""cgrp"": { ""state"": ""up"", ""rebalance_cnt"": 0, ""rebalance_age"": 0, ""assignment_size"": 1 }
        }";

        var stats = KafkaStatisticsHelper.ParseStatistics(jsonNoReq);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/no-req/heartbeat-total");
        MetricAssertions.ExpectNoKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/no-req/fetch-total");
    }

    #endregion

    #region Topic-Level Consumer Lag Average Tests

    [Test]
    public void CreateMetricsDictionary_ConsumerTopicLevel_ReportsRecordsLagAvg()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // totalConsumerLag = 10 + 15 = 25, partitionCount = 2, avg = 25 / 2 = 12
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/records-lag-avg");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/records-lag-avg"].Value, Is.EqualTo(12));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/records-lag-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerTopicLevel_ReportsConsumedTotals()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats);

        // totalRxMessages = 75 + 75 = 150, totalRxBytes = 3750 + 3750 = 7500
        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/records-consumed-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/records-consumed-total"].Value, Is.EqualTo(150));

        MetricAssertions.ExpectKey(metrics, "MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/bytes-consumed-total");
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/topic/test-topic/client/consumer-test/bytes-consumed-total"].Value, Is.EqualTo(7500));
    }

    #endregion
}
