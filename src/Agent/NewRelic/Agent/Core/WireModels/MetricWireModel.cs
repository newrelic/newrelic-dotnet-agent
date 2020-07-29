using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using Newtonsoft.Json;
using InternalMetricName = NewRelic.Agent.Core.Metric.MetricName;

namespace NewRelic.Agent.Core.WireModels
{
    [JsonConverter(typeof(JsonArrayConverter))]
    public class MetricWireModel : IAllMetricStatsCollection
    {
        [JsonArrayIndex(Index = 0)]
        public readonly MetricNameWireModel MetricName;
        [JsonArrayIndex(Index = 1)]
        public readonly MetricDataWireModel Data;

        private MetricWireModel(MetricNameWireModel metricName, MetricDataWireModel data)
        {
            MetricName = metricName;
            Data = data;
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
                throw new Exception("At least one metric must be passed in");

            var metricName = metrics.First().MetricName;

            if (metrics.Any(metric => !metric.MetricName.Equals(metricName)))
                throw new Exception("Cannot merge metrics with different names");

            var inputData = metrics.Select(metric => metric.Data);
            var mergedData = MetricDataWireModel.BuildAggregateData(inputData);
            return new MetricWireModel(metricName, mergedData);
        }
        public static MetricWireModel BuildMetric(IMetricNameService metricNameService, string proposedName, string scope, MetricDataWireModel metricData)
        {
            // MetricNameService will return null if the metric needs to be ignored
            var newName = metricNameService.RenameMetric(proposedName);
            if (newName == null)
                return null;

            var metricName = new MetricNameWireModel(newName, scope);
            return new MetricWireModel(metricName, metricData);
        }

        public override string ToString()
        {
            return MetricName.ToString() + Data.ToString();
        }

        public void AddMetricsToEngine(MetricStatsCollection engine)
        {
            if (MetricName.Scope == null || MetricName.Scope.Equals(""))
            {
                engine.MergeUnscopedStats(this);
            }
            else
            {
                engine.MergeScopedStats(MetricName.Scope, MetricName.Name, Data);
            }
        }

        public class MetricBuilder : IMetricBuilder
        {
            private readonly IMetricNameService _metricNameService;

            public MetricBuilder(IMetricNameService metricNameService)
            {
                _metricNameService = metricNameService;
            }

            #region Transaction builders
            public static void TryBuildTransactionMetrics(bool isWebTransaction, TimeSpan responseTime, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(responseTime, TimeSpan.Zero);

                var proposedName = isWebTransaction
                    ? MetricNames.WebTransactionAll
                    : MetricNames.OtherTransactionAll;
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeUnscopedStats(InternalMetricName.Create(txStats.GetTransactionName().PrefixedName), data);

                // "HttpDispacher" is a metric that is used to populate the APM response time chart. Again, response time is just EndTime minus StartTime.
                if (isWebTransaction)
                {
                    txStats.MergeUnscopedStats(MetricNames.Dispatcher, data);
                }
            }
            public static void TryBuildTotalTimeMetrics(bool isWebTransaction, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(totalTime, TimeSpan.Zero);
                var proposedName = isWebTransaction
                    ? MetricNames.WebTransactionTotalTimeAll
                    : MetricNames.OtherTransactionTotalTimeAll;
                txStats.MergeUnscopedStats(proposedName, data);

                proposedName = MetricNames.TransactionTotalTime(txStats.GetTransactionName());
                txStats.MergeUnscopedStats(proposedName, data);
            }
            public MetricWireModel TryBuildMemoryPhysicalMetric(double memoryPhysical)
            {
                var data = MetricDataWireModel.BuildByteData(memoryPhysical);
                return BuildMetric(_metricNameService, MetricNames.MemoryPhysical, null, data);
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

            #region Transaction apdex builders
            public static void TryBuildApdexMetrics(string transactionApdexName, bool isWebTransaction, TimeSpan responseTime, TimeSpan apdexT, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildApdexData(responseTime, apdexT);
                txStats.MergeUnscopedStats(InternalMetricName.Create(transactionApdexName), data);

                txStats.MergeUnscopedStats(MetricNames.ApdexAll, data);

                var proposedName = isWebTransaction
                    ? MetricNames.ApdexAllWeb
                    : MetricNames.ApdexAllOther;
                txStats.MergeUnscopedStats(proposedName, data);
            }
            public static void TryBuildFrustratedApdexMetrics(bool isWebTransaction, string txApdexName, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildFrustratedApdexData();

                txStats.MergeUnscopedStats(MetricNames.ApdexAll, data);

                var proposedName = isWebTransaction
                    ? MetricNames.ApdexAllWeb
                    : MetricNames.ApdexAllOther;
                txStats.MergeUnscopedStats(proposedName, data);

                txStats.MergeUnscopedStats(InternalMetricName.Create(txApdexName), data);
            }

            #endregion Transaction apdex builders

            #region Error metrics

            public static void TryBuildErrorsMetrics(bool isWebTransaction, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildCountData();

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

            #endregion

            #endregion Transaction builders

            #region Segment builders
            public static void TryBuildSimpleSegmentMetric(string segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDotNetInvocation(segmentName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }
            public static void TryBuildMethodSegmentMetric(string typeName, string methodName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDotNetInvocation(typeName, methodName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }
            public static void TryBuildCustomSegmentMetrics(string segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetCustom(segmentName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }
            public static void TryBuildMessageBrokerSegmentMetric(string vendor, string destination, MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetMessageBroker(destinationType, action, vendor, destination);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeScopedStats(proposedName, data);
                txStats.MergeUnscopedStats(proposedName, data);
            }
            public static void TryBuildExternalSegmentMetric(string host, string method, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats, bool unscopedOnly)
            {

                var proposedName = MetricNames.GetExternalHost(host, "Stream", method);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
                if (!unscopedOnly)
                {
                    txStats.MergeScopedStats(proposedName, data);
                }
            }

            public static void TryBuildExternalRollupMetrics(string host, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
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

            public static void TryBuildExternalAppMetric(string host, string externalCrossProcessId, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetExternalApp(host, externalCrossProcessId);
                // Note: Unlike most other metrics, this one uses exclusive time for both of its time values. We have always done it this way but it is not clear why
                var data = MetricDataWireModel.BuildTimingData(totalExclusiveTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            public static void TryBuildExternalTransactionMetric(string host, string externalCrossProcessId, string externalTransactionName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetExternalTransaction(host, externalCrossProcessId, externalTransactionName);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);

                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            public static void TryBuildClientApplicationMetric(string referrerCrossProcessId, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetClientApplication(referrerCrossProcessId);
                var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
                txStats.MergeUnscopedStats(proposedName, data);
            }


            public static void TryBuildDatastoreRollupMetrics(DatastoreVendor vendor, TimeSpan totalTime, TimeSpan exclusiveTime, TransactionMetricStatsCollection txStats)
            {
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveTime);

                // Datastore/All
                txStats.MergeUnscopedStats(MetricNames.DatastoreAll, data);

                // Datastore/<allWeb/allOther>
                var proposedName = txStats.GetTransactionName().IsWebTransactionName ? MetricNames.DatastoreAllWeb : MetricNames.DatastoreAllOther;
                txStats.MergeUnscopedStats(proposedName, data);

                // Datastore/<vendor>/all
                proposedName = MetricNames.GetDatastoreVendorAll(vendor);
                txStats.MergeUnscopedStats(proposedName, data);

                // Datastore/<vendor>/<allWeb/allOther>
                proposedName = txStats.GetTransactionName().IsWebTransactionName ? MetricNames.GetDatastoreVendorAllWeb(vendor) : MetricNames.GetDatastoreVendorAllOther(vendor);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            // Datastore/statement/<vendor>/<model>/<operation>
            public static void TryBuildDatastoreStatementMetric(DatastoreVendor vendor, string model, string operation, TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDatastoreStatement(vendor, model, operation);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
                txStats.MergeScopedStats(proposedName, data);
            }

            // Datastore/operation/<vendor>/<operation>
            public static void TryBuildDatastoreVendorOperationMetric(DatastoreVendor vendor, string operation, TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats, bool onlyUnscoped)
            {
                var proposedName = MetricNames.GetDatastoreOperation(vendor, operation);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
                if (!onlyUnscoped)
                {
                    txStats.MergeScopedStats(proposedName, data);
                }
            }

            //Datastore/instance/datastore/host/port_path_or_id
            public static void TryBuildDatastoreInstanceMetric(DatastoreVendor vendor, string host, string portPathOrId, TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
            {
                var proposedName = MetricNames.GetDatastoreInstance(vendor, host, portPathOrId);
                var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
                txStats.MergeUnscopedStats(proposedName, data);
            }

            #endregion Segment builders

            #region Supportability builders

            public MetricWireModel TryBuildDotnetVersionMetric(string version)
            {
                var proposedName = MetricNames.GetSupportabilityDotnetVersion(version);
                var data = MetricDataWireModel.BuildCountData();
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
            public MetricWireModel TryBuildMetricHarvestAttemptMetric()
            {
                const string proposedName = MetricNames.SupportabilityMetricHarvestTransmit;
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
            public MetricWireModel TryBuildTransactionBuilderGarbageCollectedRollupMetric()
            {
                const string proposedName = MetricNames.SupportabilityTransactionBuilderGarbageCollectedAll;
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, string additionalData = null)
            {
                var proposedName = MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, additionalData);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, string wrapperName, string typeName, string methodName)
            {
                var proposedName = MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, $"{wrapperName}/{typeName}.{methodName}");
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildFeatureEnabledMetric(string featureName)
            {
                var proposedName = MetricNames.GetSupportabilityFeatureEnabled(featureName);
                var data = MetricDataWireModel.BuildCountData();
                return BuildMetric(_metricNameService, proposedName, null, data);
            }

            public MetricWireModel TryBuildAgentApiMetric(string methodName)
            {
                var proposedName = MetricNames.GetSupportabilityAgentApi(methodName);
                var data = MetricDataWireModel.BuildCountData();
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

            #endregion Supportability builders
        }
    }

    public class MetricNameWireModel
    {
        [JsonProperty("name")]
        public readonly string Name;
        [JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
        public readonly string Scope;

        // We cache the hash code for MetricNameWireModel because it is guaranteed that we will need it at least once
        private readonly int _hashCode;

        public MetricNameWireModel(string name, string scope)
        {
            Name = name;
            Scope = scope;

            // See: http://stackoverflow.com/a/4630550/786388
            _hashCode = new { Name, Scope }.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            var other = obj as MetricNameWireModel;
            if (other == null)
                return false;

            return Name == other.Name && Scope == other.Scope;
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public override string ToString()
        {
            return $"{Name} ({Scope})";
        }
    }

    [JsonConverter(typeof(JsonArrayConverter))]
    public class MetricDataWireModel
    {
        [JsonArrayIndex(Index = 0)]
        public readonly long Value0;

        [JsonArrayIndex(Index = 1)]
        public readonly float Value1;

        [JsonArrayIndex(Index = 2)]
        public readonly float Value2;

        [JsonArrayIndex(Index = 3)]
        public readonly float Value3;

        [JsonArrayIndex(Index = 4)]
        public readonly float Value4;

        [JsonArrayIndex(Index = 5)]
        public readonly float Value5;

        private MetricDataWireModel(long value0, double value1, double value2, double value3, double value4, double value5)
        {
            Value0 = value0;
            Value1 = (float)value1;
            Value2 = (float)value2;
            Value3 = (float)value3;
            Value4 = (float)value4;
            Value5 = (float)value5;
        }

        public override string ToString()
        {
            return $"[{Value0},{Value1},{Value2},{Value3},{Value4},{Value5}]";
        }
        public static MetricDataWireModel BuildAggregateData(IEnumerable<MetricDataWireModel> metrics)
        {
            metrics = metrics.Where(metric => metric != null).ToList();

            var value0 = metrics.Sum(metric => metric.Value0);
            var value1 = metrics.Sum(metric => metric.Value1);
            var value2 = metrics.Sum(metric => metric.Value2);
            var value3 = metrics.Min(metric => metric.Value3);
            var value4 = metrics.Max(metric => metric.Value4);
            var value5 = metrics.Sum(metric => metric.Value5);

            return new MetricDataWireModel(value0, value1, value2, value3, value4, value5);
        }

        /// <summary>
        /// Aggregates two metric data wire models together. Always create a new one because
        /// we reuse some of the same wire models.
        /// </summary>
        /// <param name="metric0">Data to be aggregated.</param>
        /// <param name="metric1">Data to be aggregated.</param>
        /// <returns></returns>
        public static MetricDataWireModel BuildAggregateData(MetricDataWireModel metric0, MetricDataWireModel metric1)
        {
            return new MetricDataWireModel((metric0.Value0 + metric1.Value0),
                (metric0.Value1 + metric1.Value1),
                 (metric0.Value2 + metric1.Value2),
                  (Math.Min(metric0.Value3, metric1.Value3)),
                   (Math.Max(metric0.Value4, metric1.Value4)),
                    (metric0.Value5 + metric1.Value5));
        }
        public static MetricDataWireModel BuildTimingData(TimeSpan totalTime, TimeSpan totalExclusiveTime)
        {
            if (totalTime.TotalSeconds < 0)
                throw new ArgumentException("Cannot be negative", "totalTime");
            if (totalExclusiveTime.TotalSeconds < 0)
                throw new ArgumentException("Cannot be negative", "totalExclusiveTime");

            return new MetricDataWireModel(1, totalTime.TotalSeconds, totalExclusiveTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds * totalTime.TotalSeconds);
        }
        public static MetricDataWireModel BuildCountData(int callCount = 1)
        {
            if (callCount < 0)
                throw new ArgumentException("Cannot be negative", "callCount");

            return new MetricDataWireModel(callCount, 0, 0, 0, 0, 0);
        }
        public static MetricDataWireModel BuildByteData(double totalBytes, double? exclusiveBytes = null)
        {
            exclusiveBytes = exclusiveBytes ?? totalBytes;

            if (totalBytes < 0)
                throw new ArgumentException("Cannot be negative", "totalBytes");
            if (exclusiveBytes < 0)
                throw new ArgumentException("Cannot be negative", "exclusiveBytes");

            const float bytesPerMb = 1048576f;
            var totalMegabytes = totalBytes / bytesPerMb;
            var totalExclusiveMegabytes = exclusiveBytes.Value / bytesPerMb;

            return new MetricDataWireModel(1, totalMegabytes, totalExclusiveMegabytes, totalMegabytes, totalMegabytes, totalMegabytes * totalMegabytes);
        }
        public static MetricDataWireModel BuildPercentageData(float percentage)
        {
            if (percentage < 0)
                throw new ArgumentException("Cannot be negative", "percentage");
            return new MetricDataWireModel(1, percentage, percentage, percentage, percentage, percentage * percentage);
        }
        public static MetricDataWireModel BuildCpuTimeData(TimeSpan cpuTime)
        {
            if (cpuTime.TotalSeconds < 0)
                throw new ArgumentException("Cannot be negative", "cpuTime");
            return new MetricDataWireModel(1, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds * cpuTime.TotalSeconds);
        }
        public static MetricDataWireModel BuildApdexData(TimeSpan responseTime, TimeSpan apdexT)
        {
            if (responseTime.TotalSeconds < 0)
                throw new ArgumentException("Cannot be negative", "responseTime");
            if (apdexT.TotalSeconds < 0)
                throw new ArgumentException("Cannot be negative", "apdexT");

            var apdexPerfZone = GetApdexPerfZone(responseTime, apdexT);
            var satisfying = apdexPerfZone == ApdexPerfZone.Satisfying ? 1 : 0;
            var tolerating = apdexPerfZone == ApdexPerfZone.Tolerating ? 1 : 0;
            var frustrating = apdexPerfZone == ApdexPerfZone.Frustrating ? 1 : 0;

            return new MetricDataWireModel(satisfying, tolerating, frustrating, apdexT.TotalSeconds, apdexT.TotalSeconds, 0);
        }
        public static MetricDataWireModel BuildFrustratedApdexData()
        {
            return new MetricDataWireModel(0, 0, 1, 0, 0, 0);
        }
        public static MetricDataWireModel BuildIfLinuxData(bool isLinux)
        {
            return new MetricDataWireModel(1, (isLinux ? 1 : 0), 0, 0, 0, 0);
        }

        public static MetricDataWireModel BuildBootIdError()
        {
            return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
        }

        private static ApdexPerfZone GetApdexPerfZone(TimeSpan responseTime, TimeSpan apdexT)
        {
            if (responseTime.Ticks <= apdexT.Ticks)
                return ApdexPerfZone.Satisfying;

            if (responseTime.Ticks <= apdexT.Multiply(4).Ticks)
                return ApdexPerfZone.Tolerating;

            return ApdexPerfZone.Frustrating;
        }

        private enum ApdexPerfZone
        {
            Satisfying,
            Tolerating,
            Frustrating
        }
    }
}
