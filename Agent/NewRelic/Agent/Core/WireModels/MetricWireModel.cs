using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.JsonConverters;
using NewRelic.Agent.Core.Metric;
using InternalMetricName = NewRelic.Agent.Core.Metric.MetricName;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.SystemExtensions;
using Newtonsoft.Json;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof (JsonArrayConverter))]
	public class MetricWireModel: IAllMetricStatsCollection
	{
		[NotNull]
		[JsonArrayIndex(Index = 0)]
		public readonly MetricNameWireModel MetricName;

		[NotNull]
		[JsonArrayIndex(Index = 1)]
		public readonly MetricDataWireModel Data;

		private MetricWireModel([NotNull] MetricNameWireModel metricName, [NotNull] MetricDataWireModel data)
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
		[NotNull]
		public static MetricWireModel Merge(MetricWireModel first, MetricWireModel second)
		{
			return Merge(new[] {first, second});
		}

		/// <summary>
		/// Merges <paramref name="metrics"/> into one metric. At least one of them must not be null. All merged metrics must have the same metric name and scope.
		/// </summary>
		/// <param name="metrics">The metrics to merge.</param>
		/// <returns>An aggregate metric that is the result of merging the given metrics.</returns>
		[NotNull]
		public static MetricWireModel Merge([NotNull] IEnumerable<MetricWireModel> metrics)
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

		[CanBeNull]
		public static MetricWireModel BuildMetric([NotNull] IMetricNameService metricNameService, [NotNull] String proposedName, [CanBeNull] String scope, [NotNull] MetricDataWireModel metricData)
		{
			// MetricNameService will return null if the metric needs to be ignored
			var newName = metricNameService.RenameMetric(proposedName);
			if (newName == null)
				return null;

			var metricName = new MetricNameWireModel(newName, scope);
			return new MetricWireModel(metricName, metricData);
		}

		public override String ToString()
		{
			return MetricName.ToString() + Data.ToString();
		}

	   public void AddMetricsToEngine(MetricStatsCollection engine)
		{
			if (MetricName.Scope == null || MetricName.Scope.Equals(""))
			{
				engine.MergeUnscopedStats(this);
			} else
			{
				engine.MergeScopedStats(MetricName.Scope, MetricName.Name, Data);
			}
		}

		public class MetricBuilder : IMetricBuilder
		{
			[NotNull]
			private readonly IMetricNameService _metricNameService;

			public MetricBuilder([NotNull] IMetricNameService metricNameService)
			{
				_metricNameService = metricNameService;
			}

			#region Transaction builders


			[CanBeNull]
			public static void TryBuildTransactionMetrics(Boolean isWebTransaction, TimeSpan responseTime, TransactionMetricStatsCollection txStats)
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

			[CanBeNull]
			public static void TryBuildTotalTimeMetrics(Boolean isWebTransaction, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildTimingData(totalTime, TimeSpan.Zero);
				var proposedName = isWebTransaction
					? MetricNames.WebTransactionTotalTimeAll
					: MetricNames.OtherTransactionTotalTimeAll;
				txStats.MergeUnscopedStats(proposedName, data);

				proposedName = MetricNames.TransactionTotalTime(txStats.GetTransactionName());
				txStats.MergeUnscopedStats(proposedName, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildMemoryPhysicalMetric(Double memoryPhysical)
			{
				var data = MetricDataWireModel.BuildByteData(memoryPhysical);
				return BuildMetric(_metricNameService, MetricNames.MemoryPhysical, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCpuUserTimeMetric(TimeSpan cpuTime)
			{
				var data = MetricDataWireModel.BuildTimingData(cpuTime, cpuTime);
				return BuildMetric(_metricNameService, MetricNames.CpuUserTime, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCpuUserUtilizationMetric(Single cpuUtilization)
			{
				var data = MetricDataWireModel.BuildPercentageData(cpuUtilization);
				return BuildMetric(_metricNameService, MetricNames.CpuUserUtilization, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCpuTimeRollupMetric(Boolean isWebTransaction, TimeSpan cpuTime)
			{
				var proposedName = isWebTransaction
					? MetricNames.WebTransactionCpuTimeAll
					: MetricNames.OtherTransactionCpuTimeAll;
				var data = MetricDataWireModel.BuildTimingData(cpuTime, TimeSpan.Zero);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCpuTimeMetric(TransactionMetricName transactionMetricName, TimeSpan cpuTime)
			{
				var proposedName = MetricNames.TransactionCpuTime(transactionMetricName);
				var data = MetricDataWireModel.BuildTimingData(cpuTime, TimeSpan.Zero);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public static void TryBuildQueueTimeMetric(TimeSpan queueTime, TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildTimingData(queueTime, queueTime);
				txStats.MergeUnscopedStats(MetricNames.RequestQueueTime, data);
			}

			#region Transaction apdex builders

			[CanBeNull]
			public static void TryBuildApdexMetrics(String transactionApdexName, Boolean isWebTransaction, TimeSpan responseTime, TimeSpan apdexT, TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildApdexData(responseTime, apdexT);
				txStats.MergeUnscopedStats(InternalMetricName.Create(transactionApdexName), data);

				txStats.MergeUnscopedStats(MetricNames.ApdexAll, data);

				var proposedName = isWebTransaction
					? MetricNames.ApdexAllWeb
					: MetricNames.ApdexAllOther;
				txStats.MergeUnscopedStats(proposedName, data);
			}

			[CanBeNull]
			public static void TryBuildFrustratedApdexMetrics(Boolean isWebTransaction, String txApdexName, TransactionMetricStatsCollection txStats)
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

			public static void TryBuildErrorsMetrics(Boolean isWebTransaction, TransactionMetricStatsCollection txStats)
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

			[CanBeNull]
			public static void TryBuildSimpleSegmentMetric(String segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				// TODO: review this metric name (we're trying to get away from "DotNet/*" if possible)
				var proposedName = MetricNames.GetDotNetInvocation(segmentName);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			[CanBeNull]
			public static void TryBuildMethodSegmentMetric(String typeName, String methodName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				// TODO: review this metric name (we're trying to get away from "DotNet/*" if possible)
				var proposedName = MetricNames.GetDotNetInvocation(typeName, methodName);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			[CanBeNull]
			public static void TryBuildCustomSegmentMetrics(String segmentName,  TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetCustom(segmentName);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			[CanBeNull]
			public static void TryBuildMessageBrokerSegmentMetric(String vendor, String destination, MetricNames.MessageBrokerDestinationType destinationType, MetricNames.MessageBrokerAction action, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetMessageBroker(destinationType, action, vendor, destination);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeScopedStats(proposedName, data);
				txStats.MergeUnscopedStats(proposedName, data);
			}

			[CanBeNull]
			public static void TryBuildExternalSegmentMetric(String host, String method, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats, Boolean unscopedOnly)
			{

				var proposedName = MetricNames.GetExternalHost(host, "Stream", method);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
				if (!unscopedOnly)
				{
					txStats.MergeScopedStats(proposedName, data);
				}
			}

			public static void TryBuildExternalRollupMetrics(String host, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
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

			public static void TryBuildExternalAppMetric(String host, String externalCrossProcessId, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetExternalApp(host, externalCrossProcessId);
				// Note: Unlike most other metrics, this one uses exclusive time for both of its time values. We have always done it this way but it is not clear why
				var data = MetricDataWireModel.BuildTimingData(totalExclusiveTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
			}

			public static void TryBuildExternalTransactionMetric(String host, String externalCrossProcessId, String externalTransactionName, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetExternalTransaction(host, externalCrossProcessId, externalTransactionName);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
		  
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			public static void TryBuildClientApplicationMetric(String referrerCrossProcessId, TimeSpan totalTime, TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
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
			public static void TryBuildDatastoreStatementMetric(DatastoreVendor vendor, String model, String operation, TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetDatastoreStatement(vendor, model, operation);
				var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			// Datastore/operation/<vendor>/<operation>
			public static void TryBuildDatastoreVendorOperationMetric(DatastoreVendor vendor, String operation, TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats, Boolean onlyUnscoped)
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
			public static void TryBuildDatastoreInstanceMetric(DatastoreVendor vendor, String host, String portPathOrId,  TimeSpan totalTime, TimeSpan exclusiveDuration, TransactionMetricStatsCollection txStats)
			{
				var proposedName = MetricNames.GetDatastoreInstance(vendor, host, portPathOrId);
				var data = MetricDataWireModel.BuildTimingData(totalTime, exclusiveDuration);
				txStats.MergeUnscopedStats(proposedName, data);
			}

			#endregion Segment builders

			#region Supportability builders

			[CanBeNull]
			public MetricWireModel TryBuildAgentVersionMetric(String agentVersion)
			{
				var proposedName = MetricNames.GetSupportabilityAgentVersion(agentVersion);
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildAgentVersionByHostMetric(String hostName, String agentVersion)
			{
				var proposedName = MetricNames.GetSupportabilityAgentVersionByHost(hostName, agentVersion);
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildMetricHarvestAttemptMetric()
			{
				const String proposedName = MetricNames.SupportabilityMetricHarvestTransmit;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#region TransactionEvents

			[CanBeNull]
			public MetricWireModel TryBuildTransactionEventReservoirResizedMetric()
			{
				const String proposedName = MetricNames.SupportabilityTransactionEventsReservoirResize;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildTransactionEventsCollectedMetric()
			{
				const String proposedName = MetricNames.SupportabilityTransactionEventsCollected;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildTransactionEventsRecollectedMetric(Int32 eventsRecollected)
			{
				const String proposedName = MetricNames.SupportabilityTransactionEventsRecollected;
				var data = MetricDataWireModel.BuildCountData(eventsRecollected);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildTransactionEventsSentMetric(Int32 eventCount)
			{
				// Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
				const String proposedName = MetricNames.SupportabilityTransactionEventsSent;
				var data = MetricDataWireModel.BuildCountData(eventCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildTransactionEventsSeenMetric()
			{
				// Note: this metric is REQUIRED by APM (see https://source.datanerd.us/agents/agent-specs/pull/84)
				const String proposedName = MetricNames.SupportabilityTransactionEventsSeen;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion TransactionEvents

			#region CustomEvents

			[CanBeNull]
			public MetricWireModel TryBuildCustomEventReservoirResizedMetric()
			{
				const String proposedName = MetricNames.SupportabilityCustomEventsReservoirResize;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCustomEventsCollectedMetric()
			{
				const String proposedName = MetricNames.SupportabilityCustomEventsCollected;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCustomEventsRecollectedMetric(Int32 eventsRecollected)
			{
				const String proposedName = MetricNames.SupportabilityCustomEventsRecollected;
				var data = MetricDataWireModel.BuildCountData(eventsRecollected);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCustomEventsSentMetric(Int32 eventCount)
			{
				const String proposedName = MetricNames.SupportabilityCustomEventsSent;
				var data = MetricDataWireModel.BuildCountData(eventCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildCustomEventsSeenMetric()
			{
				const String proposedName = MetricNames.SupportabilityCustomEventsSeen;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion CustomEvents

			#region ErrorTraces

			[CanBeNull]
			public MetricWireModel TryBuildErrorTracesCollectedMetric()
			{
				const String proposedName = MetricNames.SupportabilityErrorTracesCollected;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildErrorTracesRecollectedMetric(Int32 errorTracesRecollected)
			{
				const String proposedName = MetricNames.SupportabilityErrorTracesRecollected;
				var data = MetricDataWireModel.BuildCountData(errorTracesRecollected);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildErrorTracesSentMetric(Int32 errorTraceCount)
			{
				const String proposedName = MetricNames.SupportabilityErrorTracesSent;
				var data = MetricDataWireModel.BuildCountData(errorTraceCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion ErrorTraces

			#region ErrorEvents

			[CanBeNull]
			public MetricWireModel TryBuildErrorEventsSentMetric(Int32 eventCount)
			{
				const String proposedName = MetricNames.SupportabilityErrorEventsSent;
				var data = MetricDataWireModel.BuildCountData(eventCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildErrorEventsSeenMetric()
			{
				const String proposedName = MetricNames.SupportabilityErrorEventsSeen;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion ErrorEvents

			#region SqlTraces

			[CanBeNull]
			public static void TryBuildSqlTracesCollectedMetric(Int32 sqlTraceCount, TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildCountData(sqlTraceCount);
				txStats.MergeUnscopedStats(MetricNames.SupportabilitySqlTracesCollected, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildSqlTracesRecollectedMetric(Int32 sqlTracesRecollected)
			{
				const String proposedName = MetricNames.SupportabilitySqlTracesRecollected;
				var data = MetricDataWireModel.BuildCountData(sqlTracesRecollected);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			[CanBeNull]
			public MetricWireModel TryBuildSqlTracesSentMetric(Int32 sqlTraceCount)
			{
				const String proposedName = MetricNames.SupportabilitySqlTracesSent;
				var data = MetricDataWireModel.BuildCountData(sqlTraceCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion SqlTraces

			[CanBeNull]
			public MetricWireModel TryBuildTransactionBuilderGarbageCollectedRollupMetric()
			{
				const String proposedName = MetricNames.SupportabilityTransactionBuilderGarbageCollectedAll;
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, String additionalData = null)
			{
				var proposedName = MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, additionalData);
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildAgentHealthEventMetric(AgentHealthEvent agentHealthEvent, String wrapperName, String typeName, String methodName)
			{
				var proposedName = MetricNames.GetSupportabilityAgentHealthEvent(agentHealthEvent, $"{wrapperName}/{typeName}.{methodName}");
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildFeatureEnabledMetric(String featureName)
			{
				var proposedName = MetricNames.GetSupportabilityFeatureEnabled(featureName);
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildAgentApiMetric(String methodName)
			{
				var proposedName = MetricNames.GetSupportabilityAgentApi(methodName);
				var data = MetricDataWireModel.BuildCountData();
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildCustomTimingMetric(String suffix, TimeSpan time)
			{
				var proposedName = MetricNames.GetCustom(suffix);
				var data = MetricDataWireModel.BuildTimingData(time, time);
				return BuildMetric(_metricNameService, proposedName.ToString(), null, data);
			}

			public MetricWireModel TryBuildCustomCountMetric(String metricName, Int32 count = 1)
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
		[NotNull]
		[JsonProperty("name")]
		public readonly String Name;

		[CanBeNull]
		[JsonProperty("scope", NullValueHandling = NullValueHandling.Ignore)]
		public readonly String Scope;

		// We cache the hash code for MetricNameWireModel because it is guaranteed that we will need it at least once
		private readonly Int32 _hashCode;

		public MetricNameWireModel([NotNull] String name, [CanBeNull] String scope)
		{
			Name = name;
			Scope = scope;

			// See: http://stackoverflow.com/a/4630550/786388
			_hashCode = new { Name, Scope }.GetHashCode();
		}

		public override Boolean Equals(Object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			var other = obj as MetricNameWireModel;
			if (other == null)
				return false;

			return Name == other.Name && Scope == other.Scope;
		}

		public override Int32 GetHashCode()
		{
			return _hashCode;
		}

		public override String ToString()
		{
			return $"{Name} ({Scope})";
		}
	}

	[JsonConverter(typeof(JsonArrayConverter))]
	public class MetricDataWireModel
	{
		[JsonArrayIndex(Index = 0)]
		public readonly Int64 Value0;

		[JsonArrayIndex(Index = 1)]
		public readonly Single Value1;

		[JsonArrayIndex(Index = 2)]
		public readonly Single Value2;

		[JsonArrayIndex(Index = 3)]
		public readonly Single Value3;

		[JsonArrayIndex(Index = 4)]
		public readonly Single Value4;

		[JsonArrayIndex(Index = 5)]
		public readonly Single Value5;

		private MetricDataWireModel(Int64 value0, Double value1, Double value2, Double value3, Double value4, Double value5)
		{
			Value0 = value0;
			Value1 = (Single)value1;
			Value2 = (Single)value2;
			Value3 = (Single)value3;
			Value4 = (Single)value4;
			Value5 = (Single)value5;
		}

		public override String ToString()
		{
			return $"[{Value0},{Value1},{Value2},{Value3},{Value4},{Value5}]";
		}

		[NotNull]
		public static MetricDataWireModel BuildAggregateData([NotNull] IEnumerable<MetricDataWireModel> metrics)
		{
			metrics = metrics.Where(metric => metric != null).ToList();

			// ReSharper disable PossibleNullReferenceException
			var value0 = metrics.Sum(metric => metric.Value0);
			var value1 = metrics.Sum(metric => metric.Value1);
			var value2 = metrics.Sum(metric => metric.Value2);
			var value3 = metrics.Min(metric => metric.Value3);
			var value4 = metrics.Max(metric => metric.Value4);
			var value5 = metrics.Sum(metric => metric.Value5);
			// ReSharper restore PossibleNullReferenceException

			return new MetricDataWireModel(value0, value1, value2, value3, value4, value5);
		}

		/// <summary>
		/// Aggregates two metric data wire models together. Always create a new one because
		/// we reuse some of the same wire models.
		/// </summary>
		/// <param name="metric0">Data to be aggregated.</param>
		/// <param name="metric1">Data to be aggregated.</param>
		/// <returns></returns>
		[NotNull]
		public static MetricDataWireModel BuildAggregateData([NotNull] MetricDataWireModel metric0, [NotNull] MetricDataWireModel metric1)
		{
			return new MetricDataWireModel((metric0.Value0 + metric1.Value0),
				(metric0.Value1 + metric1.Value1),
				 (metric0.Value2 + metric1.Value2),
				  (Math.Min(metric0.Value3, metric1.Value3)),
				   (Math.Max(metric0.Value4, metric1.Value4)),
					(metric0.Value5 + metric1.Value5));
		}

		[NotNull]
		public static MetricDataWireModel BuildTimingData(TimeSpan totalTime, TimeSpan totalExclusiveTime)
		{
			if (totalTime.TotalSeconds < 0)
				throw new ArgumentException("Cannot be negative", "totalTime");
			if (totalExclusiveTime.TotalSeconds < 0)
				throw new ArgumentException("Cannot be negative", "totalExclusiveTime");

			return new MetricDataWireModel(1, totalTime.TotalSeconds, totalExclusiveTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds * totalTime.TotalSeconds);
		}

		[NotNull]
		public static MetricDataWireModel BuildCountData(Int32 callCount = 1)
		{
			if (callCount < 0)
				throw new ArgumentException("Cannot be negative", "callCount");

			return new MetricDataWireModel(callCount, 0, 0, 0, 0, 0);
		}

		[NotNull]
		public static MetricDataWireModel BuildByteData(Double totalBytes, Double? exclusiveBytes = null)
		{
			exclusiveBytes = exclusiveBytes ?? totalBytes;

			if (totalBytes < 0)
				throw new ArgumentException("Cannot be negative", "totalBytes");
			if (exclusiveBytes < 0)
				throw new ArgumentException("Cannot be negative", "exclusiveBytes");

			const Single bytesPerMb = 1048576f;
			var totalMegabytes = totalBytes / bytesPerMb;
			var totalExclusiveMegabytes = exclusiveBytes.Value / bytesPerMb;

			return new MetricDataWireModel(1, totalMegabytes, totalExclusiveMegabytes, totalMegabytes, totalMegabytes, totalMegabytes * totalMegabytes);
		}

		[NotNull]
		public static MetricDataWireModel BuildPercentageData(Single percentage)
		{
			if (percentage < 0)
				throw new ArgumentException("Cannot be negative", "percentage");
			return new MetricDataWireModel(1, percentage, percentage, percentage, percentage, percentage * percentage);
		}

		[NotNull]
		public static MetricDataWireModel BuildCpuTimeData(TimeSpan cpuTime)
		{
			if (cpuTime.TotalSeconds < 0)
				throw new ArgumentException("Cannot be negative", "cpuTime");
			return new MetricDataWireModel(1, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds * cpuTime.TotalSeconds);
		}

		[NotNull]
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

		[NotNull]
		public static MetricDataWireModel BuildFrustratedApdexData()
		{
			return new MetricDataWireModel(0, 0, 1, 0, 0, 0);
		}

		[NotNull]
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
