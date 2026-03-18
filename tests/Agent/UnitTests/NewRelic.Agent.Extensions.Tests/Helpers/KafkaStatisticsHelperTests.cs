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
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ClientId, Is.EqualTo("producer-test"));
            Assert.That(result.ClientType, Is.EqualTo("producer"));
            Assert.That(result.RequestCount, Is.EqualTo(15));
            Assert.That(result.ResponseCount, Is.EqualTo(12));
            Assert.That(result.TxMessages, Is.EqualTo(100));
            Assert.That(result.RxMessages, Is.EqualTo(50));
            Assert.That(result.TxBytes, Is.EqualTo(5000));
            Assert.That(result.RxBytes, Is.EqualTo(2500));
            Assert.That(result.MetadataCacheCount, Is.EqualTo(3));
            Assert.That(result.MessageQueueCount, Is.EqualTo(10));
            Assert.That(result.MessageQueueSize, Is.EqualTo(250));
            Assert.That(result.TotalTxBytes, Is.EqualTo(7500));
            Assert.That(result.TotalRxBytes, Is.EqualTo(3750));
        }

        [Test]
        public void ParseStatistics_ValidConsumerJson_ReturnsCorrectData()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ClientId, Is.EqualTo("consumer-test"));
            Assert.That(result.ClientType, Is.EqualTo("consumer"));
            Assert.That(result.RequestCount, Is.EqualTo(8));
            Assert.That(result.ResponseCount, Is.EqualTo(20));
            Assert.That(result.TxMessages, Is.EqualTo(25));
            Assert.That(result.RxMessages, Is.EqualTo(150));
        }

        [Test]
        public void ParseStatistics_ValidProducerJson_ParsesProducerMetrics()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Assert
            Assert.That(result.ProducerMetrics, Is.Not.Null);
            Assert.That(result.ProducerMetrics.IdempotentState, Is.EqualTo("Init"));
            Assert.That(result.ProducerMetrics.ProducerId, Is.EqualTo(12345));
            Assert.That(result.ProducerMetrics.ProducerEpoch, Is.EqualTo(1));
            Assert.That(result.ProducerMetrics.EpochCount, Is.EqualTo(0));
            Assert.That(result.ProducerMetrics.BatchSizeAvg, Is.EqualTo(300));
            Assert.That(result.ProducerMetrics.BatchCountAvg, Is.EqualTo(3));
        }

        [Test]
        public void ParseStatistics_ValidConsumerJson_ParsesConsumerMetrics()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Assert
            Assert.That(result.ConsumerMetrics, Is.Not.Null);
            Assert.That(result.ConsumerMetrics.GroupState, Is.EqualTo("up"));
            Assert.That(result.ConsumerMetrics.RebalanceCount, Is.EqualTo(2));
            Assert.That(result.ConsumerMetrics.RebalanceAge, Is.EqualTo(30000));
            Assert.That(result.ConsumerMetrics.AssignedPartitions, Is.EqualTo(2));
            Assert.That(result.ConsumerMetrics.LastRebalanceReason, Is.EqualTo("join failed"));
            Assert.That(result.ConsumerMetrics.TotalConsumerLag, Is.EqualTo(25)); // 10 + 15 from partitions
        }

        [Test]
        public void ParseStatistics_ValidJson_ParsesBrokerMetrics()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Assert
            Assert.That(result.BrokerMetrics, Has.Count.EqualTo(1));
            var brokerMetric = result.BrokerMetrics.First();
            Assert.That(brokerMetric.BrokerName, Is.EqualTo("broker-1"));
            Assert.That(brokerMetric.NodeId, Is.EqualTo(1));
            Assert.That(brokerMetric.State, Is.EqualTo("UP"));
            Assert.That(brokerMetric.Requests, Is.EqualTo(10));
            Assert.That(brokerMetric.Responses, Is.EqualTo(8));
            Assert.That(brokerMetric.OutgoingBytes, Is.EqualTo(4000));
            Assert.That(brokerMetric.IncomingBytes, Is.EqualTo(2000));
            Assert.That(brokerMetric.RequestErrors, Is.EqualTo(1));
            Assert.That(brokerMetric.ResponseErrors, Is.EqualTo(0));
            Assert.That(brokerMetric.ConnectionCount, Is.EqualTo(1));
            Assert.That(brokerMetric.DisconnectCount, Is.EqualTo(0));
            Assert.That(brokerMetric.RoundTripTimeAvg, Is.EqualTo(10));
        }

        [Test]
        public void ParseStatistics_ValidJson_ParsesTopicMetrics()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Assert
            Assert.That(result.TopicMetrics, Has.Count.EqualTo(1));
            var topicMetric = result.TopicMetrics.First();
            Assert.That(topicMetric.TopicName, Is.EqualTo("test-topic"));
            Assert.That(topicMetric.MetadataAge, Is.EqualTo(1000));
            Assert.That(topicMetric.BatchSizeAvg, Is.EqualTo(300));
            Assert.That(topicMetric.BatchCountAvg, Is.EqualTo(3));
            Assert.That(topicMetric.PartitionCount, Is.EqualTo(1));
            Assert.That(topicMetric.TotalTxMessages, Is.EqualTo(50));
            Assert.That(topicMetric.TotalRxMessages, Is.EqualTo(25));
            Assert.That(topicMetric.TotalConsumerLag, Is.EqualTo(5));
        }

        [Test]
        public void ParseStatistics_ValidJson_ParsesPartitionMetrics()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Assert
            Assert.That(result.PartitionMetrics, Has.Count.EqualTo(1));
            var partitionMetric = result.PartitionMetrics.First();
            Assert.That(partitionMetric.TopicName, Is.EqualTo("test-topic"));
            Assert.That(partitionMetric.PartitionId, Is.EqualTo(0));
            Assert.That(partitionMetric.BrokerId, Is.EqualTo(1));
            Assert.That(partitionMetric.LeaderId, Is.EqualTo(1));
            Assert.That(partitionMetric.TxMessages, Is.EqualTo(50));
            Assert.That(partitionMetric.RxMessages, Is.EqualTo(25));
            Assert.That(partitionMetric.TxBytes, Is.EqualTo(2500));
            Assert.That(partitionMetric.RxBytes, Is.EqualTo(1250));
            Assert.That(partitionMetric.ConsumerLag, Is.EqualTo(5));
            Assert.That(partitionMetric.LowWatermark, Is.EqualTo(0));
            Assert.That(partitionMetric.HighWatermark, Is.EqualTo(100));
            Assert.That(partitionMetric.CommittedOffset, Is.EqualTo(95));
        }

        [Test]
        public void ParseStatistics_ConsumerJsonWithMultiplePartitions_CalculatesTotalConsumerLag()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Assert
            Assert.That(result.ConsumerMetrics.TotalConsumerLag, Is.EqualTo(25)); // 10 + 15 from partitions
            Assert.That(result.PartitionMetrics, Has.Count.EqualTo(2));
            Assert.That(result.PartitionMetrics.Sum(p => p.ConsumerLag), Is.EqualTo(25));
        }

        [Test]
        public void ParseStatistics_NullJson_ReturnsNull()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseStatistics_EmptyJson_ReturnsNull()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseStatistics_InvalidJson_ReturnsNull()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics("invalid json");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseStatistics_JsonWithNullResult_ReturnsNull()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics("null");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ParseStatistics_MinimalValidJson_ReturnsValidData()
        {
            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.ClientId, Is.EqualTo("minimal"));
            Assert.That(result.ClientType, Is.EqualTo("producer"));
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

            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(jsonWithoutClientId);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ClientId, Is.EqualTo("fallback-name"));
        }

        [Test]
        public void ParseStatistics_JsonMissingRequiredFields_ReturnsInvalidData()
        {
            var incompleteJson = @"{
                ""name"": ""incomplete"",
                ""tx"": 1
            }";

            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(incompleteJson);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.False); // Missing client_id and type
        }

        #endregion

        #region CreateMetricsDictionary Tests

        [Test]
        public void CreateMetricsDictionary_ValidProducerData_CreatesCorrectMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.Not.Empty);

            // Verify client-level metrics exist
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/outgoing-byte-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-send-total"));

            // Verify values
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"].Value, Is.EqualTo(15));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"].Value, Is.EqualTo(12));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"].Value, Is.EqualTo(100));
        }

        [Test]
        public void CreateMetricsDictionary_ValidConsumerData_CreatesCorrectMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.Not.Empty);

            // Verify consumer-specific metrics exist
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"));

            // Verify values
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"].Value, Is.EqualTo(2));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].Value, Is.EqualTo(2));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"].Value, Is.EqualTo(150));
        }

        [Test]
        public void CreateMetricsDictionary_ValidData_CreatesBrokerMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/response-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/connection-count"));

            // Verify values
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"].Value, Is.EqualTo(10));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"].Value, Is.EqualTo(4000));
        }

        [Test]
        public void CreateMetricsDictionary_ValidData_CreatesTopicMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total"));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-topic-metrics/topic/test-topic/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
        }

        [Test]
        public void CreateMetricsDictionary_ValidData_CreatesPartitionMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"));

            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"].Value, Is.EqualTo(50));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"].Value, Is.EqualTo(2500));
        }

        [Test]
        public void CreateMetricsDictionary_ConsumerData_CreatesPartitionLagMetrics()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"));

            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].Value, Is.EqualTo(10));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/1/client/consumer-test/records-lag"].Value, Is.EqualTo(15));
        }

        [Test]
        public void CreateMetricsDictionary_OnlyPositiveValues_AreIncluded()
        {
            // Arrange
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
            var metricsData = KafkaStatisticsHelper.ParseStatistics(jsonWithZeros);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter"));
            Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/response-counter"));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs"));
            Assert.That(metrics, Does.Not.ContainKey("MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/rxmsgs"));

            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/request-counter"].Value, Is.EqualTo(5));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/test-zeros/txmsgs"].Value, Is.EqualTo(10));
        }

        [Test]
        public void CreateMetricsDictionary_NullMetricsData_ReturnsEmptyDictionary()
        {
            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(null, "Kafka");

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.Empty);
        }

        [Test]
        public void CreateMetricsDictionary_InvalidMetricsData_ReturnsEmptyDictionary()
        {
            // Arrange
            var invalidData = new KafkaStatisticsHelper.KafkaMetricsData
            {
                ClientId = null, // Invalid - no client ID
                ClientType = "producer"
            };

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(invalidData, "Kafka");

            // Assert
            Assert.That(metrics, Is.Not.Null);
            Assert.That(metrics, Is.Empty);
        }

        [Test]
        public void CreateMetricsDictionary_CustomVendorName_UsesCorrectVendorName()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(MinimalValidJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "CustomKafka");

            // Assert
            Assert.That(metrics.Keys.First(), Does.Contain("MessageBroker/CustomKafka/Internal/"));
        }

        #endregion

        #region Edge Cases and Utility Methods

        [Test]
        public void IsValid_ValidData_ReturnsTrue()
        {
            // Arrange
            var data = new KafkaStatisticsHelper.KafkaMetricsData
            {
                ClientId = "test-client",
                ClientType = "producer"
            };

            // Act & Assert
            Assert.That(data.IsValid, Is.True);
        }

        [Test]
        public void IsValid_MissingClientId_ReturnsFalse()
        {
            // Arrange
            var data = new KafkaStatisticsHelper.KafkaMetricsData
            {
                ClientId = null,
                ClientType = "producer"
            };

            // Act & Assert
            Assert.That(data.IsValid, Is.False);
        }

        [Test]
        public void IsValid_EmptyClientId_ReturnsFalse()
        {
            // Arrange
            var data = new KafkaStatisticsHelper.KafkaMetricsData
            {
                ClientId = "",
                ClientType = "producer"
            };

            // Act & Assert
            Assert.That(data.IsValid, Is.False);
        }

        [Test]
        public void IsValid_MissingClientType_ReturnsFalse()
        {
            // Arrange
            var data = new KafkaStatisticsHelper.KafkaMetricsData
            {
                ClientId = "test-client",
                ClientType = null
            };

            // Act & Assert
            Assert.That(data.IsValid, Is.False);
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

            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(minimalJson);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.BrokerMetrics, Is.Empty);
            Assert.That(result.TopicMetrics, Is.Empty);
            Assert.That(result.PartitionMetrics, Is.Empty);
            Assert.That(result.ConsumerMetrics.TotalConsumerLag, Is.EqualTo(0));
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

            // Act
            var result = KafkaStatisticsHelper.ParseStatistics(producerWithoutBatchesJson);
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(result, "Kafka");

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ProducerMetrics, Is.Not.Null);
            Assert.That(result.ProducerMetrics.BatchSizeAvg, Is.EqualTo(0));
            Assert.That(result.ProducerMetrics.BatchCountAvg, Is.EqualTo(0));
            Assert.That(metrics, Contains.Key("MessageBroker/Kafka/Internal/producer-metrics/client/producer-no-batch/request-counter"));
        }

        #endregion

        #region Metric Type Classification Tests

        [Test]
        public void CreateMetricsDictionary_CumulativeCounters_AreTaggedAsCumulative()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert — client-level cumulative counters
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/request-counter"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/response-counter"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/txmsgs"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/outgoing-byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));

            // Assert — broker-level cumulative counters
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/outgoing-byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));

            // Assert — partition-level cumulative counters
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/record-send-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/topic/test-topic/partition/0/client/producer-test/byte-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        }

        [Test]
        public void CreateMetricsDictionary_GaugeValues_AreTaggedAsGauge()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert — client-level gauges
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/metadata_cache_cnt"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/record-queue-time-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        }

        [Test]
        public void CreateMetricsDictionary_ConsumerGaugeValues_AreTaggedAsGauge()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert — consumer coordinator gauges
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/assigned-partitions"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));

            // Assert — consumer lag gauges
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-lag-max"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));

            // Assert — partition-level lag gauges
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-metrics/topic/test-topic/partition/0/client/consumer-test/records-lag"].MetricType, Is.EqualTo(KafkaMetricType.Gauge));
        }

        [Test]
        public void CreateMetricsDictionary_WindowAverages_AreTaggedAsWindowAvg()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidProducerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert — producer window averages
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-metrics/client/producer-test/batch-size-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));

            // Assert — broker RTT window average
            Assert.That(metrics["MessageBroker/Kafka/Internal/producer-node-metrics/node/1/client/producer-test/request-latency-avg"].MetricType, Is.EqualTo(KafkaMetricType.WindowAvg));
        }

        [Test]
        public void CreateMetricsDictionary_ConsumerCumulativeCounters_AreTaggedAsCumulative()
        {
            // Arrange
            var metricsData = KafkaStatisticsHelper.ParseStatistics(ValidConsumerStatisticsJson);

            // Act
            var metrics = KafkaStatisticsHelper.CreateMetricsDictionary(metricsData, "Kafka");

            // Assert — consumer cumulative counters
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-coordinator-metrics/client/consumer-test/rebalance-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/records-consumed-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
            Assert.That(metrics["MessageBroker/Kafka/Internal/consumer-fetch-manager-metrics/client/consumer-test/bytes-consumed-total"].MetricType, Is.EqualTo(KafkaMetricType.Cumulative));
        }

        #endregion
    }