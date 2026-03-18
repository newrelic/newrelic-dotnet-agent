// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
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
                ""state"": ""UP"",
                ""tx"": 5,
                ""rx"": 15,
                ""txbytes"": 1000,
                ""rxbytes"": 5000,
                ""txerrs"": 0,
                ""rxerrs"": 1,
                ""connects"": 1,
                ""disconnects"": 0
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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(result, "Kafka");

        // BatchSizeAvg is computed from topics and reported as a metric
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg"));
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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(result, "Kafka");

        // Total consumer lag (10 + 15 = 25) is reported as records-lag-max
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"].Value, Is.EqualTo(25));

        // Per-partition lag
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"));
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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Not.Empty);

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/outgoing-byte-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-send-total"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"].Value, Is.EqualTo(15));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"].Value, Is.EqualTo(12));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"].Value, Is.EqualTo(100));
    }

    [Test]
    public void CreateMetricsDictionary_ValidConsumerData_CreatesCorrectMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Not.Empty);

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"].Value, Is.EqualTo(2));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].Value, Is.EqualTo(2));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"].Value, Is.EqualTo(150));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesBrokerMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/response-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/connection-count"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"].Value, Is.EqualTo(4000));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesTopicMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total"));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
    }

    [Test]
    public void CreateMetricsDictionary_ValidData_CreatesPartitionMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"].Value, Is.EqualTo(2500));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerData_CreatesPartitionLagMetrics()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].Value, Is.EqualTo(10));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"].Value, Is.EqualTo(15));
    }

    [Test]
    public void CreateMetricsDictionary_OnlyPositiveValues_AreIncluded()
    {
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
            ""metadata_cache_cnt"": 1
        }";
        var stats = KafkaStatisticsHelper.ParseStatistics(jsonWithZeros);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter"));
        Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/response-counter"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs"));
        Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/rxmsgs"));

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter"].Value, Is.EqualTo(5));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs"].Value, Is.EqualTo(10));
    }

    [Test]
    public void CreateMetricsDictionary_NullStats_ReturnsEmptyDictionary()
    {
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(null, "Kafka");

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

        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(invalidStats, "Kafka");

        Assert.That(metrics, Is.Not.Null);
        Assert.That(metrics, Is.Empty);
    }

    [Test]
    public void CreateMetricsDictionary_CustomVendorName_UsesCorrectVendorName()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "CustomKafka");

        Assert.That(metrics.Keys.First(), Does.Contain("MessageBroker/CustomKafka/Internal/"));
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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(stats, Is.Not.Null);
        // No batch-size-avg metric when batch metrics are absent
        Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/producer-no-batch/batch-size-avg"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-no-batch/request-counter"));
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

        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats, "Kafka");
        var firstCount = metrics.Count;
        Assert.That(firstCount, Is.GreaterThan(0));

        // Populate again — should clear and refill
        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats, "Kafka");
        Assert.That(metrics.Count, Is.EqualTo(firstCount));
    }

    [Test]
    public void PopulateMetricsDictionary_ClearsPreviousEntries()
    {
        var metrics = new System.Collections.Generic.Dictionary<string, KafkaMetricValue>();
        metrics["stale/metric"] = new KafkaMetricValue(999, KafkaMetricType.Gauge);

        var stats = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);
        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, stats, "Kafka");

        Assert.That(metrics, Does.Not.ContainKey("stale/metric"));
    }

    [Test]
    public void PopulateMetricsDictionary_NullStats_ClearsDictionary()
    {
        var metrics = new System.Collections.Generic.Dictionary<string, KafkaMetricValue>();
        metrics["existing"] = new KafkaMetricValue(1, KafkaMetricType.Gauge);

        KafkaStatisticsHelper.PopulateMetricsDictionary(metrics, null, "Kafka");

        Assert.That(metrics, Is.Empty);
    }

    #endregion

    #region Metric Type Classification Tests

    [Test]
    public void CreateMetricsDictionary_CumulativeCounters_AreTaggedAsCumulative()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/metadata_cache_cnt"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-queue-time-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerGaugeValues_AreTaggedAsGauge()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
    }

    [Test]
    public void CreateMetricsDictionary_WindowAverages_AreTaggedAsWindowAvg()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));
        Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));
    }

    [Test]
    public void CreateMetricsDictionary_ConsumerCumulativeCounters_AreTaggedAsCumulative()
    {
        var stats = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

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
        var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(stats, "Kafka");

        // Client-level should have request-counter but NOT request-total (was a duplicate)
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"));
        Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-total"));
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"));
        Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-total"));

        // Node-level should still have request-total (not a duplicate — different scope)
        Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"));
    }

    #endregion
}
