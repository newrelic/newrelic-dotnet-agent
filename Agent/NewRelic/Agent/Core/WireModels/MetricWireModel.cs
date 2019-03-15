using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using NewRelic.Agent.Core.SharedInterfaces;
using InternalMetricName = NewRelic.Agent.Core.Metric.MetricName;
using NewRelic.Agent.Core.JsonConverters;

namespace NewRelic.Agent.Core.WireModels
{
	[JsonConverter(typeof(MetricWireModelCollectionJsonConverter))]
	public class MetricWireModelCollection
	{
		public string AgentRunID { get; private set; }
		public double StartEpochTime { get; private set; }
		public double EndEpochTime { get; private set; }
		public IEnumerable<MetricWireModel> Metrics { get; private set; }

		public MetricWireModelCollection(string agentRunId, double beginEpoch, double endEpoch, IEnumerable<MetricWireModel> metrics)
		{
			AgentRunID = agentRunId;
			StartEpochTime = beginEpoch;
			EndEpochTime = endEpoch;
			Metrics = metrics;
		}
	}

	[JsonConverter(typeof(MetricWireModelJsonConverter))]
	public class MetricWireModel : IAllMetricStatsCollection
	{
		public readonly MetricNameWireModel MetricName;
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
			{
				throw new Exception("At least one metric must be passed in");
			}

			var metricName = metrics.First().MetricName;

			if (metrics.Any(metric => !metric.MetricName.Equals(metricName)))
			{
				throw new Exception("Cannot merge metrics with different names");
			}

			var inputData = metrics.Select(metric => metric.Data);
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
			return MetricName + Data.ToString();
		}

		public void AddMetricsToEngine(MetricStatsCollection engine)
		{
			if (string.IsNullOrEmpty(MetricName.Scope))
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


			public static void TryBuildTransactionMetrics(bool isWebTransaction, TimeSpan responseTimeOrDuration,
				TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildTimingData(responseTimeOrDuration, TimeSpan.Zero);

				var proposedName = isWebTransaction
					? MetricNames.WebTransactionAll
					: MetricNames.OtherTransactionAll;
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeUnscopedStats(InternalMetricName.Create(txStats.GetTransactionName().PrefixedName), data);

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

			public static void TryBuildApdexMetrics(string transactionApdexName, bool isWebTransaction, TimeSpan responseTime,
				TimeSpan apdexT, TransactionMetricStatsCollection txStats)
			{
				var data = MetricDataWireModel.BuildApdexData(responseTime, apdexT);
				txStats.MergeUnscopedStats(InternalMetricName.Create(transactionApdexName), data);

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

			public static void TryBuildSimpleSegmentMetric(string segmentName, TimeSpan totalTime, TimeSpan totalExclusiveTime,
				TransactionMetricStatsCollection txStats)
			{
				// TODO: review this metric name (we're trying to get away from "DotNet/*" if possible)
				var proposedName = MetricNames.GetDotNetInvocation(segmentName);
				var data = MetricDataWireModel.BuildTimingData(totalTime, totalExclusiveTime);
				txStats.MergeUnscopedStats(proposedName, data);
				txStats.MergeScopedStats(proposedName, data);
			}

			public static void TryBuildMethodSegmentMetric(string typeName, string methodName, TimeSpan totalTime,
				TimeSpan totalExclusiveTime, TransactionMetricStatsCollection txStats)
			{
				// TODO: review this metric name (we're trying to get away from "DotNet/*" if possible)
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
				var proposedName = InternalMetricName.Create(sqlStatement.DatastoreStatementMetricName);
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
				const string proposedName = MetricNames.SupportabilitySpanEventsSeen;
				var data = MetricDataWireModel.BuildCountData(eventCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildSpanEventsSentMetric(int eventCount)
			{
				const string proposedName = MetricNames.SupportabilitySpanEventsSent;
				var data = MetricDataWireModel.BuildCountData(eventCount);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildSqlParsingCacheCountMetric(string name, int count)
			{
				var proposedName = MetricNames.SupportabilitySqlParsingCachePrefix + MetricNames.PathSeparator + name;
				var data = MetricDataWireModel.BuildCountData(count);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			public MetricWireModel TryBuildSqlParsingCacheSizeMetric(string name, int size)
			{
				var proposedName = MetricNames.SupportabilitySqlParsingCachePrefix + MetricNames.PathSeparator + name;
				var data = MetricDataWireModel.BuildAverageData(size);
				return BuildMetric(_metricNameService, proposedName, null, data);
			}

			#endregion Span builders
		}
	}

	[JsonConverter(typeof(MetricNameWireModelJsonConverter))]
	public class MetricNameWireModel
	{
		private const string PropertyName = "name";
		private const string PropertyScope = "scope";

		// property name: "name"
		public readonly string Name;

		// property name: "scope"
		public readonly string Scope;

		// We cache the hash code for MetricNameWireModel because it is guaranteed that we will need it at least once
		private readonly int _hashCode;
		private static int HashCodeCombiner(int h1, int h2)
		{
			var rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
			return ((int)rol5 + h1) ^ h2;
		}

		public MetricNameWireModel(string name, string scope)
		{
			Name = name;
			Scope = scope;

			//no heap allocation to compute hash code
			_hashCode = HashCodeCombiner(Name.GetHashCode(), Scope?.GetHashCode() ?? 0);
		}

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj))
				return true;

			return obj is MetricNameWireModel other && Name == other.Name && Scope == other.Scope;
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

	[JsonConverter(typeof(MetricDataWireModelJsonConverter))]
	public class MetricDataWireModel
	{
		private const string CannotBeNegative = "Cannot be negative";

		public readonly long Value0;
		public readonly float Value1;
		public readonly float Value2;
		public readonly float Value3;
		public readonly float Value4;
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

		public static MetricDataWireModel BuildAggregateData(IEnumerable<MetricDataWireModel> metrics)
		{
			long value0 = 0;
			float value1 = 0, value2 = 0, value3 = float.MaxValue, value4 = float.MinValue, value5 = 0;

			foreach (var metric in metrics)
			{
				if (metric == null)
					continue;

				value0 += metric.Value0;
				value1 += metric.Value1;
				value2 += metric.Value2;
				value3 = Math.Min(value3, metric.Value3);
				value4 = Math.Max(value4, metric.Value4);
				value5 += metric.Value5;
			}

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
			{
				throw new ArgumentException(CannotBeNegative, nameof(totalTime));
			}

			if (totalExclusiveTime.TotalSeconds < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(totalExclusiveTime));
			}

			return new MetricDataWireModel(1, totalTime.TotalSeconds, totalExclusiveTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds, totalTime.TotalSeconds * totalTime.TotalSeconds);
		}

		public static MetricDataWireModel BuildCountData(int callCount = 1)
		{
			if (callCount < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(callCount));
			}

			return new MetricDataWireModel(callCount, 0, 0, 0, 0, 0);
		}

		public static MetricDataWireModel BuildByteData(double totalBytes, double? exclusiveBytes = null)
		{
			exclusiveBytes = exclusiveBytes ?? totalBytes;

			if (totalBytes < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(totalBytes));
			}

			if (exclusiveBytes < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(exclusiveBytes));
			}

			const float bytesPerMb = 1048576f;
			var totalMegabytes = totalBytes / bytesPerMb;
			var totalExclusiveMegabytes = exclusiveBytes.Value / bytesPerMb;

			return new MetricDataWireModel(1, totalMegabytes, totalExclusiveMegabytes, totalMegabytes, totalMegabytes, totalMegabytes * totalMegabytes);
		}

		public static MetricDataWireModel BuildPercentageData(float percentage)
		{
			if (percentage < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(percentage));
			}

			return new MetricDataWireModel(1, percentage, percentage, percentage, percentage, percentage * percentage);
		}

		public static MetricDataWireModel BuildCpuTimeData(TimeSpan cpuTime)
		{
			if (cpuTime.TotalSeconds < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(cpuTime));
			}

			return new MetricDataWireModel(1, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds, cpuTime.TotalSeconds * cpuTime.TotalSeconds);
		}

		public static MetricDataWireModel BuildApdexData(TimeSpan responseTime, TimeSpan apdexT)
		{
			if (responseTime.TotalSeconds < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(responseTime));
			}

			if (apdexT.TotalSeconds < 0)
			{
				throw new ArgumentException(CannotBeNegative, nameof(apdexT));
			}

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

		public static MetricDataWireModel BuildAwsUsabilityError()
		{
			return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
		}

		public static MetricDataWireModel BuildAzureUsabilityError()
		{
			return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
		}

		public static MetricDataWireModel BuildPcfUsabilityError()
		{
			return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
		}

		public static MetricDataWireModel BuildGcpUsabilityError()
		{
			return new MetricDataWireModel(1, 0, 0, 0, 0, 0);
		}

		public static MetricDataWireModel BuildAverageData(float value)
		{
			return new MetricDataWireModel(1, value, value, value, value, value * value);
		}

		private static ApdexPerfZone GetApdexPerfZone(TimeSpan responseTime, TimeSpan apdexT)
		{
			var ticks = responseTime.Ticks;
			if (ticks <= apdexT.Ticks)
			{
				return ApdexPerfZone.Satisfying;
			}

			return ticks <= apdexT.Multiply(4).Ticks ? ApdexPerfZone.Tolerating : ApdexPerfZone.Frustrating;
		}

		private enum ApdexPerfZone
		{
			Satisfying,
			Tolerating,
			Frustrating
		}
	}
}
