// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Samplers;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(MetricWireModelJsonConverter))]
    public class MetricWireModel : IAllMetricStatsCollection, IWireModel
    {
        public readonly MetricNameWireModel MetricNameModel;
        public readonly MetricDataWireModel DataModel;

        private MetricWireModel(MetricNameWireModel metricNameModel, MetricDataWireModel dataModel)
        {
            MetricNameModel = metricNameModel;
            DataModel = dataModel;
        }

        /// <summary>
        /// Merges <paramref name="first"/> and <paramref name="second"/> into one metric. At least one of them must not be null. All merged metrics must have the same metric name and scope.
        /// </summary>
        /// <param name="first">The first metric to merge.</param>
        /// <param name="second">The second metric to merge.</param>
        /// <returns>An aggregate metric that is the result of merging the given metrics.</returns>
        public static MetricWireModel Merge(MetricWireModel first, MetricWireModel second)
        {
            return Merge(new[] { first, second });
        }

        /// <summary>
        /// Merges <paramref name="metrics"/> into one metric. At least one of them must not be null. All merged metrics must have the same metric name and scope.
        /// </summary>
        /// <param name="metrics">The metrics to merge.</param>
        /// <returns>An aggregate metric that is the result of merging the given metrics.</returns>
        public static MetricWireModel Merge(IEnumerable<MetricWireModel> metrics)
        {
            metrics = metrics.Where(other => other != null).ToList();
            if (!metrics.Any())
            {
                throw new Exception("At least one metric must be passed in");
            }

            var metricName = metrics.First().MetricNameModel;
            if (metrics.Any(metric => !metric.MetricNameModel.Equals(metricName)))
            {
                throw new Exception("Cannot merge metrics with different names");
            }

            var inputData = metrics.Select(metric => metric.DataModel);
            var mergedData = MetricDataWireModel.BuildAggregateData(inputData);
            return new MetricWireModel(metricName, mergedData);
        }

        public static MetricWireModel BuildMetric(IMetricNameService metricNameService, string proposedName, string scope, MetricDataWireModel metricData)
        {
            // MetricNameService will return null if the metric needs to be ignored
            var newName = metricNameService.RenameMetric(proposedName);
            if (newName == null)
            {
                return null;
            }

            var metricName = new MetricNameWireModel(newName, scope);
            return new MetricWireModel(metricName, metricData);
        }

        public override string ToString()
        {
            return MetricNameModel + DataModel.ToString();
        }

        public void AddMetricsToCollection(MetricStatsCollection collection)
        {
            if (string.IsNullOrEmpty(MetricNameModel.Scope))
            {
                collection.MergeUnscopedStats(MetricNameModel.Name, DataModel);
            }
            else
            {
                collection.MergeScopedStats(MetricNameModel.Scope, MetricNameModel.Name, DataModel);
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is MetricWireModel other)
            {
                return other.MetricNameModel.Equals(this.MetricNameModel) && other.DataModel.Equals(this.DataModel);
            }

            return false;
        }

        public override int GetHashCode()
        {
            var hashCode = 2074576463;
            hashCode = hashCode * -1521134295 + EqualityComparer<MetricNameWireModel>.Default.GetHashCode(MetricNameModel);
            hashCode = hashCode * -1521134295 + EqualityComparer<MetricDataWireModel>.Default.GetHashCode(DataModel);
            return hashCode;
        }

        public class MetricBuilder : IMetricBuilder
        {
            private readonly IMetricNameService _metricNameService;

            public MetricBuilder(IMetricNameService metricNameService)
            {
                _metricNameService = metricNameService;
            }

            #region Transaction builders

            public static void TryBuildTransactionMetrics(bool isWebTransaction, TimeSpan responseTimeOrDuration,
                TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(responseTimeOrDuration, TimeSpan.Zero);
                var proposedName = isWebTransaction
                    ? MetricNames.WebTransactionAll
                    : MetricNames.OtherTransactionAll;
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeUnscopedStats(MetricName.Create(txStats.GetTransactionName().PrefixedName), data);

                // "HttpDispacher" is a metric that is used to populate the APM response time chart.
                if (isWebTransaction)
                {
                    txStats.MergeUnscopedStats(MetricNames.Dispatcher, data);
                }
            }

            public static void TryBuildTotalTimeMetrics(bool isWebTransaction, TimeSpan totalTime,
                TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(totalTime, TimeSpan.Zero);
                var proposedName = isWebTransaction
                    ? MetricNames.WebTransactionTotalTimeAll
                    : MetricNames.OtherTransactionTotalTimeAll;
                txStats.MergeUnscopedStats(proposedName, data);
                proposedName = MetricNames.TransactionTotalTime(txStats.GetTransactionName());
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public MetricWireModel TryBuildMemoryPhysicalMetric(long memoryPhysical)
            {
                var data = MetricDataWireModel.BuildByteData(memoryPhysical);
                return BuildMetric(_metricNameService, MetricNames.MemoryPhysical, null, data);
            }

            public MetricWireModel TryBuildMemoryWorkingSetMetric(long memoryWorkingSet)
            {
                var data = MetricDataWireModel.BuildByteData(memoryWorkingSet);
                return BuildMetric(_metricNameService, MetricNames.MemoryWorkingSet, null, data);
            }

            public MetricWireModel TryBuildThreadpoolUsageStatsMetric(ThreadType type, ThreadStatus status, int countThreadpoolThreads)
            {
                var data = MetricDataWireModel.BuildGaugeValue(countThreadpoolThreads);
                return BuildMetric(_metricNameService, MetricNames.GetThreadpoolUsageStatsName(type, status), null, data);
            }

            public MetricWireModel TryBuildThreadpoolThroughputStatsMetric(ThreadpoolThroughputStatsType type, int statsVal)
            {
                var data = MetricDataWireModel.BuildGaugeValue(statsVal);
                return BuildMetric(_metricNameService, MetricNames.GetThreadpoolThroughputStatsName(type), null, data);
            }

            public MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime)
            {
                var data = MetricDataWireModel.BuildTimingData(cpuTime, cpuTime);
                return BuildMetric(_metricNameService, MetricNames.CpuUserTime, null, data);
            }

            public MetricWireModel TryBuildCpuUserUtilizationMetric(float cpuUtilization)
            {
                var data = MetricDataWireModel.BuildPercentageData(cpuUtilization);
                return BuildMetric(_metricNameService, MetricNames.CpuUserUtilization, null, data);
            }

            public MetricWireModel TryBuildCpuTimeRollupMetric(bool isWebTransaction, TimeSpan cpuTime)
            {
                var proposedName = isWebTransaction
                    ? MetricNames.WebTransactionCpuTimeAll
                    : MetricNames.OtherTransactionCpuTimeAll;
                var data = MetricDataWireModel.BuildTimingData(cpuTime, TimeSpan.Zero);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime)
            {
                var proposedName = MetricNames.TransactionCpuTime(transactionMetricName);
                var data = MetricDataWireModel.BuildTimingData(cpuTime, TimeSpan.Zero);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public static void TryBuildQueueTimeMetric(TimeSpan queueTime, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(queueTime, queueTime);
                txStats.MergeUnscopedStats(MetricNames.RequestQueueTime, data);
            }

            public MetricWireModel TryBuildGCBytesMetric(GCSampleType sampleType, long value)
            {
                var data = MetricDataWireModel.BuildByteData(value);
                return BuildMetric(_metricNameService, MetricNames.GetGCMetricName(sampleType), null, data);
            }

            public MetricWireModel TryBuildGCCountMetric(GCSampleType sampleType, int value)
            {
                var data = MetricDataWireModel.BuildCountData(value);
                return BuildMetric(_metricNameService, MetricNames.GetGCMetricName(sampleType), null, data);
            }

            public MetricWireModel TryBuildGCPercentMetric(GCSampleType sampleType, float value)
            {
                var data = MetricDataWireModel.BuildPercentageData(value);
                return BuildMetric(_metricNameService, MetricNames.GetGCMetricName(sampleType), null, data);
            }

            public MetricWireModel TryBuildGCGaugeMetric(GCSampleType sampleType, float value)
            {
                var data = MetricDataWireModel.BuildGaugeValue(value);
                return BuildMetric(_metricNameService, MetricNames.GetGCMetricName(sampleType), null, data);
            }

            #region Transaction apdex builders

            public static void TryBuildApdexMetrics(string transactionApdexName, bool isWebTransaction, TimeSpan responseTime,
                TimeSpan apdexT, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildApdexData(responseTime, apdexT);
                txStats.MergeUnscopedStats(MetricName.Create(transactionApdexName), data);
                txStats.MergeUnscopedStats(MetricNames.ApdexAll, data);
                var proposedName = isWebTransaction
                    ? MetricNames.ApdexAllWeb
                    : MetricNames.ApdexAllOther;
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildFrustratedApdexMetrics(bool isWebTransaction, string txApdexName,
                TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildFrustratedApdexData();
                txStats.MergeUnscopedStats(MetricNames.ApdexAll, data);
                var proposedName = isWebTransaction
                    ? MetricNames.ApdexAllWeb
                    : MetricNames.ApdexAllOther;
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeUnscopedStats(MetricName.Create(txApdexName), data);
            }

            #endregion Transaction apdex builders

            #region Error metrics

            public static void TryBuildErrorsMetrics(bool isWebTransaction, TransactionMetricStatsCollection txStats, bool isErrorExpected)
            {
                var data = MetricDataWireModel.BuildCountData();

                if (!isErrorExpected)
                {
                    //All metric
                    txStats.MergeUnscopedStats(MetricNames.ErrorsAll, data);

                    //all web/other metric
                    var proposedName = isWebTransaction
                        ? MetricNames.ErrorsAllWeb
                        : MetricNames.ErrorsAllOther;
                    txStats.MergeUnscopedStats(proposedName, data);

                    //transaction error metric
                    proposedName = MetricNames.GetErrorTransaction(txStats.GetTransactionName().PrefixedName);
                    txStats.MergeUnscopedStats(proposedName, data);
                }
                else
                {
                    txStats.MergeUnscopedStats(MetricNames.ErrorsExpectedAll, data);
                }
            }

            #endregion

            #region Kafka metrics

            public static void TryBuildKafkaMessagesReceivedMetric(string transactionName, int count, TransactionMetricStatsCollection txStats)
            {
                var parts = transactionName.Split('/');
                var proposedName = MetricNames.GetKafkaMessagesReceivedPerConsume(parts.Last());
                var data = MetricDataWireModel.BuildCountData(count);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            #endregion Kafka metrics

            #endregion Transaction builders

            #region Segment builders

            public static void TryBuildSimpleSegmentMetric(string segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime,
                TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDotNetInvocation(segmentName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            public static void TryBuildMethodSegmentMetric(string typeName, string methodName, TimeSpan totalTime,
                TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDotNetInvocation(typeName, methodName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            public static void TryBuildCustomSegmentMetrics(string segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime,
                TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetCustom(segmentName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            public static void TryBuildMessageBrokerSegmentMetric(string vendor, string destination,
                MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action,
                TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetMessageBroker(destinationType, action, vendor, destination);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeScopedStats(proposedName, data);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildMessageBrokerSerializationSegmentMetric(string vendor, string destination,
                MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action, string kind,
                TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetMessageBrokerSerialization(destinationType, action, vendor, destination, kind);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeScopedStats(proposedName, data);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildExternalSegmentMetric(string host, string method, TimeSpan totalTime,
                TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats, bool unscopedOnly)
            {
                var proposedName = MetricNames.GetExternalHost(host, "Stream", method);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                if (!unscopedOnly)
                {
                    txStats.MergeScopedStats(proposedName, data);
                }
            }

            public static void TryBuildExternalRollupMetrics(string host, TimeSpan totalTime,
                TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalTime);
                txStats.MergeUnscopedStats(MetricNames.ExternalAll, data);
                var proposedName = txStats.GetTransactionName().IsWebTransactionName
                    ? MetricNames.ExternalAllWeb
                    : MetricNames.ExternalAllOther;
                txStats.MergeUnscopedStats(proposedName, data);
                proposedName = MetricNames.GetExternalHostRollup(host);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildExternalAppMetric(string host, string externalCrossProcessId, TimeSpan totalExclusiveTime,
                TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetExternalApp(host, externalCrossProcessId);
                // Note: Unlike most other metrics, this one uses exclusive time for both of its time values. We have always done it this way but it is not clear why
                var data = MetricDataWireModel.BuildTimingData(totalExclusiveTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildExternalTransactionMetric(string host, string externalCrossProcessId,
                string externalTransactionName, TimeSpan totalTime, TimeSpan totalExclusiveTime,
                TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetExternalTransaction(host, externalCrossProcessId, externalTransactionName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            public static void TryBuildClientApplicationMetric(string referrerCrossProcessId, TimeSpan totalTime,
                TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetClientApplication(referrerCrossProcessId);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
            }


            public static void TryBuildDatastoreRollupMetrics(DatastoreVendor vendor, TimeSpan totalTime, TimeSpan exclusiveTime,
                TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveTime);

                // Datastore/All
                txStats.MergeUnscopedStats(MetricNames.DatastoreAll, data);

                // Datastore/<allWeb/allOther>
                var proposedName = txStats.GetTransactionName().IsWebTransactionName
                    ? MetricNames.DatastoreAllWeb
                    : MetricNames.DatastoreAllOther;
                txStats.MergeUnscopedStats(proposedName, data);

                // Datastore/<vendor>/all
                proposedName = vendor.GetDatastoreVendorAll();
                txStats.MergeUnscopedStats(proposedName, data);

                // Datastore/<vendor>/<allWeb/allOther>
                proposedName = txStats.GetTransactionName().IsWebTransactionName
                    ? vendor.GetDatastoreVendorAllWeb()
                    : vendor.GetDatastoreVendorAllOther();
                txStats.MergeUnscopedStats(proposedName, data);
            }

            // Datastore/statement/<vendor>/<model>/<operation>
            public static void TryBuildDatastoreStatementMetric(DatastoreVendor vendor, ParsedSqlStatement sqlStatement,
                TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricName.Create(sqlStatement.DatastoreStatementMetricName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            // Datastore/operation/<vendor>/<operation>
            public static void TryBuildDatastoreVendorOperationMetric(DatastoreVendor vendor, string operation,
                TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats, bool onlyUnscoped)
            {
                var proposedName = vendor.GetDatastoreOperation(operation);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
                if (!onlyUnscoped)
                {
                    txStats.MergeScopedStats(proposedName, data);
                }
            }

            //Datastore/instance/datastore/host/port_path_or_id
            public static void TryBuildDatastoreInstanceMetric(DatastoreVendor vendor, string host, string portPathOrId,
                TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDatastoreInstance(vendor, host, portPathOrId);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            #endregion Segment builders

            #region Supportability builders

            public MetricWireModel TryBuildSupportabilityCountMetric(string metricName, long count)
            {
                var proposedName = MetricNames.GetSupportabilityName(metricName);
                var data = MetricDataWireModel.BuildCountData(count);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityDataUsageMetric(string metricName, long callCount, float dataSent, float dataReceived)
            {
                var data = MetricDataWireModel.BuildDataUsageValue(callCount, dataSent, dataReceived);
                return BuildMetric(_metricNameService, metricName, null, data);
            }

            public MetricWireModel TryBuildSupportabilitySummaryMetric(string metricName, float totalValue, int countSamples, float minValue, float maxValue)
            {
                var proposedName = MetricNames.GetSupportabilityName(metricName);
                var data = MetricDataWireModel.BuildSummaryValue(countSamples, totalValue, minValue, maxValue);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityGaugeMetric(string metricName, float value)
            {
                var proposedName = MetricNames.GetSupportabilityName(metricName);
                var data = MetricDataWireModel.BuildGaugeValue(value);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildDotnetFrameworkVersionMetric(DotnetFrameworkVersion version)
            {
                var proposedName = MetricNames.GetSupportabilityDotnetFrameworkVersion(version);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildDotnetCoreVersionMetric(DotnetCoreVersion version)
            {
                var proposedName = MetricNames.GetSupportabilityDotnetCoreVersion(version);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCATSupportabilityCountMetric(CATSupportabilityCondition conditionType, int count)
            {
                var proposedName = MetricNames.GetSupportabilityCATConditionMetricName(conditionType);
                var data = MetricDataWireModel.BuildCountData(count);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentVersionMetric(string agentVersion)
            {
                var proposedName = MetricNames.GetSupportabilityAgentVersion(agentVersion);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentVersionByHostMetric(string hostName, string agentVersion)
            {
                var proposedName = MetricNames.GetSupportabilityAgentVersionByHost(hostName, agentVersion);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }
            public MetricWireModel TryBuildLibraryVersionMetric(string assemblyName, string assemblyVersion)
            {
                var proposedName = MetricNames.GetSupportabilityLibraryVersion(assemblyName, assemblyVersion);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildMetricHarvestAttemptMetric()
            {
                const string proposedName = MetricNames.SupportabilityMetricHarvestTransmit;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildInstallTypeMetric(string installType)
            {
                var proposedName = MetricNames.GetSupportabilityInstallType(installType);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #region TransactionEvents

            public MetricWireModel TryBuildTransactionEventReservoirResizedMetric()
            {
                const string proposedName = MetricNames.SupportabilityTransactionEventsReservoirResize;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildTransactionEventsCollectedMetric()
            {
                const string proposedName = MetricNames.SupportabilityTransactionEventsCollected;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildTransactionEventsRecollectedMetric(int eventsRecollected)
            {
                const string proposedName = MetricNames.SupportabilityTransactionEventsRecollected;
                var data = MetricDataWireModel.BuildCountData(eventsRecollected);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildTransactionEventsSentMetric(int eventCount)
            {
                // Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
                const string proposedName = MetricNames.SupportabilityTransactionEventsSent;
                var data = MetricDataWireModel.BuildCountData(eventCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildTransactionEventsSeenMetric()
            {
                // Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
                const string proposedName = MetricNames.SupportabilityTransactionEventsSeen;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #endregion TransactionEvents

            #region CustomEvents

            public MetricWireModel TryBuildCustomEventReservoirResizedMetric()
            {
                const string proposedName = MetricNames.SupportabilityCustomEventsReservoirResize;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCustomEventsCollectedMetric()
            {
                const string proposedName = MetricNames.SupportabilityCustomEventsCollected;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCustomEventsRecollectedMetric(int eventsRecollected)
            {
                const string proposedName = MetricNames.SupportabilityCustomEventsRecollected;
                var data = MetricDataWireModel.BuildCountData(eventsRecollected);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCustomEventsSentMetric(int eventCount)
            {
                const string proposedName = MetricNames.SupportabilityCustomEventsSent;
                var data = MetricDataWireModel.BuildCountData(eventCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCustomEventsSeenMetric()
            {
                const string proposedName = MetricNames.SupportabilityCustomEventsSeen;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #endregion CustomEvents

            #region ErrorTraces

            public MetricWireModel TryBuildErrorTracesCollectedMetric()
            {
                const string proposedName = MetricNames.SupportabilityErrorTracesCollected;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildErrorTracesRecollectedMetric(int errorTracesRecollected)
            {
                const string proposedName = MetricNames.SupportabilityErrorTracesRecollected;
                var data = MetricDataWireModel.BuildCountData(errorTracesRecollected);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildErrorTracesSentMetric(int errorTraceCount)
            {
                const string proposedName = MetricNames.SupportabilityErrorTracesSent;
                var data = MetricDataWireModel.BuildCountData(errorTraceCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #endregion ErrorTraces

            #region ErrorEvents

            public MetricWireModel TryBuildErrorEventsSentMetric(int eventCount)
            {
                const string proposedName = MetricNames.SupportabilityErrorEventsSent;
                var data = MetricDataWireModel.BuildCountData(eventCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildErrorEventsSeenMetric()
            {
                const string proposedName = MetricNames.SupportabilityErrorEventsSeen;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #endregion ErrorEvents

            #region SqlTraces

            public static void TryBuildSqlTracesCollectedMetric(int sqlTraceCount, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildCountData(sqlTraceCount);
                txStats.MergeUnscopedStats(MetricNames.SupportabilitySqlTracesCollected, data);
            }

            public MetricWireModel TryBuildSqlTracesRecollectedMetric(int sqlTracesRecollected)
            {
                const string proposedName = MetricNames.SupportabilitySqlTracesRecollected;
                var data = MetricDataWireModel.BuildCountData(sqlTracesRecollected);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSqlTracesSentMetric(int sqlTraceCount)
            {
                const string proposedName = MetricNames.SupportabilitySqlTracesSent;
                var data = MetricDataWireModel.BuildCountData(sqlTraceCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            #endregion SqlTraces

            public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent,
                string additionalData = null)
            {
                var proposedName = MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, additionalData);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, string wrapperName,
                string typeName, string methodName)
            {
                var proposedName =
                    MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, $"{wrapperName}/{typeName}.{methodName}");
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildFeatureEnabledMetric(string featureName)
            {
                var proposedName = MetricNames.GetSupportabilityFeatureEnabled(featureName);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }


            public MetricWireModel TryBuildAgentApiMetric(string methodName, int count)
            {
                var proposedName = MetricNames.GetSupportabilityAgentApi(methodName);
                var data = MetricDataWireModel.BuildCountData(count);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCustomTimingMetric(string suffix, TimeSpan time)
            {
                var proposedName = MetricNames.GetCustom(suffix);
                var data = MetricDataWireModel.BuildTimingData(time, time);
                return BuildMetric(_metricNameService, proposedName.ToString(), null, data);
            }

            public MetricWireModel TryBuildCustomCountMetric(string metricName, int count = 1)
            {
                // NOTE: Unlike Custom timing metrics, Custom count metrics are NOT restricted to only the "Custom" namespace.
                // This is probably a historical blunder -- it's not a good thing that we allow users to use whatever text they want for the first segment.
                // However, that is what the API currently allows and it would be difficult to take that feature away.
                var data = MetricDataWireModel.BuildCountData(count);
                return BuildMetric(_metricNameService, metricName, null, data);
            }

            public MetricWireModel TryBuildLinuxOsMetric(bool isLinux)
            {
                var proposedName = MetricNames.GetSupportabilityLinuxOs();
                var data = MetricDataWireModel.BuildIfLinuxData(isLinux);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildBootIdError()
            {
                var proposedName = MetricNames.GetSupportabilityBootIdError();
                var data = MetricDataWireModel.BuildBootIdError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildKubernetesUsabilityError()
            {
                var proposedName = MetricNames.GetSupportabilityKubernetesUsabilityError();
                var data = MetricDataWireModel.BuildKubernetesUsabilityError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAwsUsabilityError()
            {
                var proposedName = MetricNames.GetSupportabilityAwsUsabilityError();
                var data = MetricDataWireModel.BuildAwsUsabilityError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAzureUsabilityError()
            {
                var proposedName = MetricNames.GetSupportabilityAzureUsabilityError();
                var data = MetricDataWireModel.BuildAzureUsabilityError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildPcfUsabilityError()
            {
                var proposedName = MetricNames.GetSupportabilityPcfUsabilityError();
                var data = MetricDataWireModel.BuildPcfUsabilityError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildGcpUsabilityError()
            {
                var proposedName = MetricNames.GetSupportabilityGcpUsabilityError();
                var data = MetricDataWireModel.BuildGcpUsabilityError();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentTimingMetric(string suffix, TimeSpan time)
            {
                var proposedName = MetricNames.GetSupportabilityAgentTimingMetric(suffix);
                var data = MetricDataWireModel.BuildTimingData(time, time);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }


            private MetricWireModel TryBuildSupportabilityDistributedTraceMetric(string proposedName, int count = 1) =>
                BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));

            // New Relic Payload (Legacy DT) sup. metrics: https://source.datanerd.us/agents/agent-specs/blob/master/distributed_tracing/New-Relic-Payload.md

            /// <summary>Created during harvest if one or more payloads were accepted.</summary>
            public MetricWireModel TryBuildAcceptPayloadSuccess(int count) =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadSuccess, count);

            /// <summary>Created when AcceptDistributedTracePayload had a generic exception</summary>
            public MetricWireModel TryBuildAcceptPayloadException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadException);

            /// <summary>Created when AcceptDistributedTracePayload had a parsing exception</summary>
            public MetricWireModel TryBuildAcceptPayloadParseException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadParseException);

            /// <summary>Created when AcceptDistributedTracePayload was ignored because CreatePayload had already been called</summary>
            public MetricWireModel TryBuildAcceptPayloadIgnoredCreateBeforeAccept =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredCreateBeforeAccept);

            /// <summary>Created when AcceptDistributedTracePayload was ignored because AcceptPayload had already been called</summary>
            public MetricWireModel TryBuildAcceptPayloadIgnoredMultiple =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMultiple);

            /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload's major version was greater than the agent's</summary>
            public MetricWireModel TryBuildAcceptPayloadIgnoredMajorVersion =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredMajorVersion);

            /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was null</summary>
            public MetricWireModel TryBuildAcceptPayloadIgnoredNull =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredNull);

            /// <summary>Created when AcceptDistributedTracePayload was ignored because the payload was untrusted</summary>
            public MetricWireModel TryBuildAcceptPayloadIgnoredUntrustedAccount() =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceAcceptPayloadIgnoredUntrustedAccount);

            /// <summary>Created during harvest when one or more payloads are created.</summary>
            public MetricWireModel TryBuildCreatePayloadSuccess(int count) =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceCreatePayloadSuccess, count);

            /// <summary>Created when CreateDistributedTracePayload had a generic exception</summary>
            public MetricWireModel TryBuildCreatePayloadException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityDistributedTraceCreatePayloadException);

            // Trace Context Supportability Metrics: https://source.datanerd.us/agents/agent-specs/blob/master/distributed_tracing/Trace-Context-Payload.md

            /// <summary>The agent successfully accepted inbound traceparent and tracestate headers.</summary>
            public MetricWireModel TryBuildTraceContextAcceptSuccess(int count) =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextAcceptSuccess, count);

            /// <summary>The agent successfully created the outbound payloads.</summary>
            public MetricWireModel TryBuildTraceContextCreateSuccess(int count) =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextCreateSuccess, count);

            /// <summary>A generic exception occurred unrelated to parsing while accepting either payload.</summary>
            public MetricWireModel TryBuildTraceContextAcceptException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextAcceptException);

            /// <summary>The inbound traceparent header could not be parsed.</summary>
            public MetricWireModel TryBuildTraceContextTraceParentParseException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextTraceParentParseException);

            /// <summary>The inbound tracestate header could not be parsed.</summary>
            public MetricWireModel TryBuildTraceContextTraceStateParseException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextTraceStateParseException);

            /// <summary>A generic exception occurred while creating the outbound payloads.</summary>
            public MetricWireModel TryBuildTraceContextCreateException =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextCreateException);

            /// <summary>The inbound tracestate header exists, and was accepted, but the New Relic entry was invalid.</summary>
            public MetricWireModel TryBuildTraceContextTraceStateInvalidNrEntry =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextTraceStateInvalidNrEntry);

            /// <summary>The traceparent header exists, and was accepted, but the tracestate header did not contain a trusted New Relic entry.</summary>
            public MetricWireModel TryBuildTraceContextTraceStateNoNrEntry =>
                TryBuildSupportabilityDistributedTraceMetric(MetricNames.SupportabilityTraceContextTraceStateNoNrEntry);


            public MetricWireModel TryBuildSupportabilityErrorHttpStatusCodeFromCollector(HttpStatusCode statusCode)
            {
                var proposedName = MetricNames.GetSupportabilityErrorHttpStatusCodeFromCollector(statusCode);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityEndpointMethodErrorAttempts(string endpointMethod)
            {
                var proposedName = MetricNames.GetSupportabilityEndpointMethodErrorAttempts(endpointMethod);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityEndpointMethodErrorDuration(string endpointMethod, TimeSpan responseDuration)
            {
                var proposedName = MetricNames.GetSupportabilityEndpointMethodErrorDuration(endpointMethod);
                var data = MetricDataWireModel.BuildTimingData(responseDuration, responseDuration);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            private MetricWireModel TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimitMetric(string proposedName, int count) =>
                BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));

            public MetricWireModel TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimit(string endpoint, int count = 1) =>
                TryBuildSupportabilityPayloadsDroppedDueToMaxPayloadLimitMetric(MetricNames.GetSupportabilityPayloadsDroppedDueToMaxPayloadLimit(endpoint), count);

            #endregion Supportability builders

            #region Distributed Trace builders

            public static void TryBuildDistributedTraceDurationByCaller(string type, string accountId, string app, string transport, bool isWeb, TimeSpan duration, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(duration, TimeSpan.Zero);

                var (all, webOrOther) = MetricNames.GetDistributedTraceDurationByCaller(type, accountId, app, transport, isWeb);
                txStats.MergeUnscopedStats(all, data);
                txStats.MergeUnscopedStats(webOrOther, data);
            }

            public static void TryBuildDistributedTraceErrorsByCaller(string type, string accountId, string app, string transport, bool isWeb, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildCountData();
                var (all, webOrOther) = MetricNames.GetDistributedTraceErrorsByCaller(type, accountId, app, transport, isWeb);
                txStats.MergeUnscopedStats(all, data);
                txStats.MergeUnscopedStats(webOrOther, data);
            }

            public static void TryBuildDistributedTraceTransportDuration(string type, string accountId, string app, string transport, bool isWeb, TimeSpan duration, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(duration, TimeSpan.Zero);

                var (all, webOrOther) = MetricNames.GetDistributedTraceTransportDuration(type, accountId, app, transport, isWeb);
                txStats.MergeUnscopedStats(all, data);
                txStats.MergeUnscopedStats(webOrOther, data);
            }

            #endregion Distributed Trace builders

            #region Span builders

            public MetricWireModel TryBuildSpanEventsSeenMetric(int eventCount)
            {
                return TryBuildSupportabilityCountMetric(MetricNames.SupportabilitySpanEventsSeen, eventCount);
            }

            public MetricWireModel TryBuildSpanEventsSentMetric(int eventCount)
            {
                return TryBuildSupportabilityCountMetric(MetricNames.SupportabilitySpanEventsSent, eventCount);
            }

            #endregion Span builders

            #region Log Events and Metrics

            public MetricWireModel TryBuildLoggingMetricsLinesCountBySeverityMetric(string logLevel, int count)
            {
                var proposedName = MetricNames.GetLoggingMetricsLinesBySeverityName(logLevel);
                return BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));
            }

            public MetricWireModel TryBuildLoggingMetricsLinesCountMetric(int count)
            {
                var proposedName = MetricNames.GetLoggingMetricsLinesName();
                return BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));
            }

            public MetricWireModel TryBuildLoggingMetricsDeniedCountBySeverityMetric(string logLevel, int count)
            {
                var proposedName = MetricNames.GetLoggingMetricsDeniedBySeverityName(logLevel);
                return BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));
            }

            public MetricWireModel TryBuildLoggingMetricsDeniedCountMetric(int count)
            {
                var proposedName = MetricNames.GetLoggingMetricsDeniedName();
                return BuildMetric(_metricNameService, proposedName, null, MetricDataWireModel.BuildCountData(count));
            }

            public MetricWireModel TryBuildSupportabilityLoggingEventsCollectedMetric()
            {
                const string proposedName = MetricNames.SupportabilityLoggingEventsCollected;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityLoggingEventsSentMetric(int loggingEventCount)
            {
                const string proposedName = MetricNames.SupportabilityLoggingEventsSent;
                var data = MetricDataWireModel.BuildCountData(loggingEventCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildSupportabilityLoggingEventsDroppedMetric(int droppedCount)
            {
                const string proposedName = MetricNames.SupportabilityLoggingEventsDropped;
                var data = MetricDataWireModel.BuildCountData(droppedCount);
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildCountMetric(string metricName, long count)
            {
                var data = MetricDataWireModel.BuildCountData(count);
                return BuildMetric(_metricNameService, metricName, null, data);
            }

            public MetricWireModel TryBuildByteMetric(string metricName, long totalBytes, long? exclusiveBytes)
            {
                var data = MetricDataWireModel.BuildByteData(totalBytes, exclusiveBytes);
                return BuildMetric(_metricNameService, metricName, null, data);
            }

            #endregion
        }
    }
}
