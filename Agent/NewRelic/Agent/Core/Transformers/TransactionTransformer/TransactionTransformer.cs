using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Errors;
using static NewRelic.Agent.Core.WireModels.MetricWireModel;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ITransactionTransformer
	{
		void Transform([NotNull] ITransaction transaction);
	}

	public class TransactionTransformer : ITransactionTransformer
	{
		[NotNull]
		private readonly ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private readonly ISegmentTreeMaker _segmentTreeMaker;

		[NotNull]
		private readonly IMetricNameService _metricNameService;

		[NotNull]
		private readonly IMetricAggregator _metricAggregator;

		[NotNull]
		private readonly IConfigurationService _configurationService;

		[NotNull]
		private readonly ITransactionTraceAggregator _transactionTraceAggregator;

		[NotNull]
		private readonly ITransactionTraceMaker _transactionTraceMaker;

		[NotNull]
		private readonly ITransactionEventAggregator _transactionEventAggregator;

		[NotNull]
		private readonly ITransactionEventMaker _transactionEventMaker;

		private readonly ISpanEventAggregator _spanEventAggregator;

		private readonly ISpanEventMaker _spanEventMaker;

		[NotNull]
		private readonly ITransactionAttributeMaker _transactionAttributeMaker;

		[NotNull]
		private readonly IErrorTraceAggregator _errorTraceAggregator;

		[NotNull]
		private readonly IErrorTraceMaker _errorTraceMaker;

		[NotNull]
		private readonly IErrorEventAggregator _errorEventAggregator;

		[NotNull]
		private readonly IErrorEventMaker _errorEventMaker;

		[NotNull]
		private readonly ISqlTraceAggregator _sqlTraceAggregator;

		[NotNull]
		private readonly ISqlTraceMaker _sqlTraceMaker;

		private readonly IAgentTimerService _agentTimerService;

		public TransactionTransformer([NotNull] ITransactionMetricNameMaker transactionMetricNameMaker, [NotNull] ISegmentTreeMaker segmentTreeMaker, [NotNull] IMetricNameService metricNameService, [NotNull] IMetricAggregator metricAggregator, [NotNull] IConfigurationService configurationService, [NotNull] ITransactionTraceAggregator transactionTraceAggregator, [NotNull] ITransactionTraceMaker transactionTraceMaker, [NotNull] ITransactionEventAggregator transactionEventAggregator, [NotNull] ITransactionEventMaker transactionEventMaker, [NotNull] ITransactionAttributeMaker transactionAttributeMaker, [NotNull] IErrorTraceAggregator errorTraceAggregator, [NotNull] IErrorTraceMaker errorTraceMaker, IErrorEventAggregator errorEventAggregator, IErrorEventMaker errorEventMaker, ISqlTraceAggregator sqlTraceAggregator, ISqlTraceMaker sqlTraceMaker, ISpanEventAggregator spanEventAggregator, ISpanEventMaker spanEventMaker, IAgentTimerService agentTimerService)
		{
			_transactionMetricNameMaker = transactionMetricNameMaker;
			_segmentTreeMaker = segmentTreeMaker;
			_metricNameService = metricNameService;
			_metricAggregator = metricAggregator;
			_configurationService = configurationService;
			_transactionTraceAggregator = transactionTraceAggregator;
			_transactionTraceMaker = transactionTraceMaker;
			_transactionEventAggregator = transactionEventAggregator;
			_transactionEventMaker = transactionEventMaker;
			_transactionAttributeMaker = transactionAttributeMaker;
			_errorTraceAggregator = errorTraceAggregator;
			_errorTraceMaker = errorTraceMaker;
			_errorEventAggregator = errorEventAggregator;
			_errorEventMaker = errorEventMaker;
			_sqlTraceAggregator = sqlTraceAggregator;
			_sqlTraceMaker = sqlTraceMaker;
			_spanEventAggregator = spanEventAggregator;
			_spanEventMaker = spanEventMaker;
			_agentTimerService = agentTimerService;
		}

		public void Transform(ITransaction transaction)
		{
			if (transaction.Ignored)
			{
				return;
			}

			ComputeSampled(transaction.TransactionMetadata);

			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			
			// Note: Metric names are normally handled internally by the IMetricBuilder. However, transactionMetricName is an exception because (sadly) it is used for more than just metrics. For example, transaction events need to use metric name, as does RUM and CAT.
			var transactionMetricName = _transactionMetricNameMaker.GetTransactionMetricName(immutableTransaction.TransactionName);
			if (transactionMetricName.ShouldIgnore)
			{
				Log.FinestFormat("Transaction \"{0}\" is being ignored due to metric naming rules", transactionMetricName);
				return;
			}

			using (_agentTimerService.StartNew("Transform", transactionMetricName.PrefixedName))
			{
				Transform(immutableTransaction, transactionMetricName);
			}

			Log.FinestFormat("Transaction {0} ({1}) transform completed.", transaction.Guid, transactionMetricName);
		}

		private void Transform(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName)
		{
			if (!immutableTransaction.Segments.Any())
				throw new ArgumentException("Transaction does not have any segments");

			FinishSegments(immutableTransaction.Segments);

			TryGenerateExplainPlans(immutableTransaction.Segments);

			var totalTime = GetTotalExclusiveTime(immutableTransaction.Segments);
			var transactionApdexMetricName = MetricNames.GetTransactionApdex(transactionMetricName);
			var apdexT = GetApdexT(immutableTransaction, transactionMetricName.PrefixedName);

			var txStats = new TransactionMetricStatsCollection(transactionMetricName);
			var errorData = ErrorData.TryGetErrorData(immutableTransaction, _configurationService);
			
			GenerateAndCollectSqlTrace(immutableTransaction, transactionMetricName, txStats);
			GenerateAndCollectMetrics(immutableTransaction, errorData.IsAnError, apdexT, transactionApdexMetricName, totalTime, txStats);
			
			// defer the creation of attributes until something asks for them.
			Func<Attributes> attributes = () => _transactionAttributeMaker.GetAttributes(immutableTransaction, transactionMetricName, apdexT, totalTime, errorData, txStats);
			attributes = attributes.Memoize();

			// Must generate errors first so other wire models get attribute updates
			if (errorData.IsAnError) {
				GenerateAndCollectErrorEventTracesAndEvents(immutableTransaction, attributes.Invoke(), transactionMetricName, errorData);
			}

			GenerateAndCollectTransactionEvent(immutableTransaction, attributes);

			GenerateAndCollectTransactionTrace(immutableTransaction, transactionMetricName, attributes);

			GenerateAndCollectSpanEvents(immutableTransaction, transactionMetricName.PrefixedName);
		}

		private static void FinishSegments([NotNull] IEnumerable<Segment> segments)
		{
			Stack<Segment> unfinishedSegments = new Stack<Segment>();
			foreach (var segment in segments)
			{
				if (segment.RelativeEndTime.HasValue == false)
				{
					unfinishedSegments.Push(segment);
				}
			}

			// if we have to force any segments to finish, do it in the reverse order of their creation
			// to guarantee that children are finished before their parents
			foreach (var segment in unfinishedSegments)
			{
				// if the segment ended between being added to the unfinished list and now, this call to 
				// end will have no effect
				segment.ForceEnd();
				Log.FinestFormat("Force segment to finish for method {0}", segment.MethodCallData);
			}
		}

		private static TimeSpan GetTotalExclusiveTime([NotNull] IEnumerable<Segment> segments)
		{
			long total = 0;

			foreach (var segment in segments)
			{
				total += segment.ExclusiveDurationOrZero.Ticks;
			}

			return new TimeSpan(total);
		}

		[CanBeNull]
		private ErrorTraceWireModel GenerateErrorTrace([NotNull] ImmutableTransaction immutableTransaction, [NotNull] Attributes attributes, TransactionMetricName transactionMetricName, ErrorData errorData)
		{
			if (!_configurationService.Configuration.ErrorCollectorEnabled)
				return null;

			return _errorTraceMaker.GetErrorTrace(immutableTransaction, attributes, transactionMetricName, errorData);
		}

		private TimeSpan? GetApdexT([NotNull] ImmutableTransaction immutableTransaction, [NotNull] String transactionApdexMetricName)
		{
			var apdexT = _metricNameService.TryGetApdex_t(transactionApdexMetricName);
			if (immutableTransaction.IsWebTransaction())
				apdexT = apdexT ?? _configurationService.Configuration.TransactionTraceApdexT;

			return apdexT;
		}

		private void GenerateAndCollectErrorEventTracesAndEvents([NotNull] ImmutableTransaction immutableTransaction, [NotNull] Attributes attributes, TransactionMetricName transactionMetricName, ErrorData errorData)
		{
			var errorTrace = GenerateErrorTrace(immutableTransaction, attributes, transactionMetricName, errorData);
			if (errorTrace == null)
				return;

			using (_agentTimerService.StartNew("CollectErrorTrace"))
			{
				_errorTraceAggregator.Collect(errorTrace);
			}

			if (_configurationService.Configuration.ErrorCollectorCaptureEvents) {
				var errorEvent = _errorEventMaker.GetErrorEvent(errorData, immutableTransaction, attributes);
				using (_agentTimerService.StartNew("CollectErrorEvent"))
				{
					_errorEventAggregator.Collect(errorEvent);
				}
			}
		}

		private void GenerateAndCollectMetrics([NotNull] ImmutableTransaction immutableTransaction, Boolean isErrorTranasction, TimeSpan? apdexT, [NotNull] String transactionApdexMetricName, TimeSpan totalTime, TransactionMetricStatsCollection txStats)
		{
			foreach (var segment in immutableTransaction.Segments)
			{
				GenerateSegmentMetrics(segment, txStats);
			}

			var isWebTransaction = immutableTransaction.IsWebTransaction();
			var immutableTransactionMetadata = immutableTransaction.TransactionMetadata;

			var isDistributedTracingEnabled = _configurationService.Configuration.DistributedTracingEnabled;

			var duration = immutableTransactionMetadata.DistributedTraceTransportDuration;
			var type = immutableTransactionMetadata.DistributedTraceType;
			var account = immutableTransactionMetadata.DistributedTraceAccountId;
			var app = immutableTransactionMetadata.DistributedTraceAppId;
			var transport = immutableTransactionMetadata.DistributedTraceTransportType;

			{
				// Response time is just EndTime minus StartTime
				MetricBuilder.TryBuildTransactionMetrics(isWebTransaction, immutableTransaction.Duration, txStats);

				if (isDistributedTracingEnabled)
				{
					MetricBuilder.TryBuildDistributedTraceDurationByCaller(type, account, app, transport, isWebTransaction,
						immutableTransaction.Duration, txStats);

					if (immutableTransactionMetadata.HasIncomingDistributedTracePayload)
					{
						MetricBuilder.TryBuildDistributedTraceTransportDuration(type, account, app, transport, isWebTransaction, duration, txStats);
					}
				}

				// Total time is the total amount of time spent, even when work is happening parallel, which means it is the sum of all exclusive times.
				// https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
				MetricBuilder.TryBuildTotalTimeMetrics(isWebTransaction, totalTime, txStats);

				// CPU time is the total time spent actually doing work rather than waiting. Basically, it's TotalTime minus TimeSpentWaiting.
				// Our agent does not yet the ability to calculate time spent waiting, so we cannot generate this metric.
				// https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
				//_metricBuilder.TryBuildCpuTimeRollupMetric(isWebTransaction, immutableTransaction.Duration, txStats),
				//_metricBuilder.TryBuildCpuTimeMetric(transactionMetricName, immutableTransaction.Duration, txStats)
			}

			if (immutableTransaction.TransactionMetadata.QueueTime != null)
				MetricBuilder.TryBuildQueueTimeMetric(immutableTransaction.TransactionMetadata.QueueTime.Value, txStats);

			if (apdexT != null && !immutableTransaction.IgnoreApdex)
			{
				GetApdexMetrics(immutableTransaction, isErrorTranasction, apdexT.Value, transactionApdexMetricName, txStats);
			}

			if (isErrorTranasction)
			{
				MetricBuilder.TryBuildErrorsMetrics(isWebTransaction, txStats);
				if (isDistributedTracingEnabled)
				{
					MetricBuilder.TryBuildDistributedTraceErrorsByCaller(type, account, app, transport, isWebTransaction, txStats);
				}
			}

			var referrerCrossProcessId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId;
			if (referrerCrossProcessId != null)
				MetricBuilder.TryBuildClientApplicationMetric(referrerCrossProcessId, immutableTransaction.Duration, immutableTransaction.Duration, txStats);

			using (_agentTimerService.StartNew("CollectMetrics"))
			{
				_metricAggregator.Collect(txStats);
			}
		}

		private void GenerateAndCollectTransactionEvent([NotNull] ImmutableTransaction immutableTransaction, [NotNull] Func<Attributes> attributes)
		{
			if (!_configurationService.Configuration.TransactionEventsEnabled)
				return;

			if (!_configurationService.Configuration.TransactionEventsTransactionsEnabled)
				return;

			var transactionEvent = _transactionEventMaker.GetTransactionEvent(immutableTransaction, attributes.Invoke());
			using (_agentTimerService.StartNew("CollectTransactionEvent"))
			{
				_transactionEventAggregator.Collect(transactionEvent);
			}
		}

		private void GenerateAndCollectSpanEvents(ImmutableTransaction immutableTransaction, string transactionName)
		{
			if (!_configurationService.Configuration.SpanEventsEnabled)
				return;

			if (immutableTransaction.TransactionMetadata.DistributedTraceSampled.HasValue && immutableTransaction.TransactionMetadata.DistributedTraceSampled.Value == false)
				return;

			var spanEvents = _spanEventMaker.GetSpanEvents(immutableTransaction, transactionName);
			using (_agentTimerService.StartNew("CollectSpanEvents"))
			{
				_spanEventAggregator.Collect(spanEvents);
			}
		}

		private void GenerateAndCollectTransactionTrace([NotNull] ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, [NotNull] Func<Attributes> attributes)
		{
			if (!_configurationService.Configuration.TransactionTracerEnabled)
				return;

			var traceComponents = new TransactionTraceWireModelComponents(
							transactionMetricName,
							immutableTransaction.Duration,
							immutableTransaction.TransactionMetadata.IsSynthetics,
							() => _transactionTraceMaker.GetTransactionTrace(immutableTransaction, _segmentTreeMaker.BuildSegmentTrees(immutableTransaction.Segments), transactionMetricName, attributes.Invoke()));

			using (_agentTimerService.StartNew("CollectTransactionTrace"))
			{
				_transactionTraceAggregator.Collect(traceComponents);
			}
		}

		private void GenerateAndCollectSqlTrace([NotNull] ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TransactionMetricStatsCollection txStats)
		{
			if (!_configurationService.Configuration.SlowSqlEnabled)
				return;

			var txSqlTrStats = new SqlTraceStatsCollection();

			foreach(var segment in immutableTransaction.Segments.OfType<TypedSegment<DatastoreSegmentData>>())
			{
				if ( segment.TypedData.CommandText != null &&
					 segment.Duration >= _configurationService.Configuration.SqlExplainPlanThreshold)
				{
					AddSqlTraceStats(txSqlTrStats, _sqlTraceMaker.TryGetSqlTrace(immutableTransaction, transactionMetricName, segment));
				}
			}

			if (txSqlTrStats.Collection.Count > 0)
			{
				using (_agentTimerService.StartNew("CollectSqlTrace"))
				{
					_sqlTraceAggregator.Collect(txSqlTrStats);
				}

				MetricBuilder.TryBuildSqlTracesCollectedMetric(txSqlTrStats.TracesCollected, txStats);
			}
		}

		private void TryGenerateExplainPlans(IEnumerable<Segment> segments)
		{
			if (!_configurationService.Configuration.SqlExplainPlansEnabled)
			{
				return;
			}
			try
			{
				using (new IgnoreWork())
				{
					short count = 0;
					var sqlExplainPlansMax = _configurationService.Configuration.SqlExplainPlansMax;
					var threshold = _configurationService.Configuration.SqlExplainPlanThreshold;
					var obfuscator = SqlObfuscator.GetSqlObfuscator(_configurationService.Configuration.TransactionTracerEnabled, _configurationService.Configuration.TransactionTracerRecordSql);
					foreach (var segment in segments)
					{
						if (segment.Duration > threshold && segment is TypedSegment<DatastoreSegmentData>)
						{
							var datastoreSegmentData = ((TypedSegment<DatastoreSegmentData>)segment).TypedData;
							if (datastoreSegmentData.DoExplainPlanCondition?.Invoke() == true)
							{
								datastoreSegmentData.ExecuteExplainPlan(obfuscator);
								if (++count >= sqlExplainPlansMax)
								{
									return;
								}
							}
						}
					}
				}
			}
			catch (Exception exception)
			{
				Log.DebugFormat("Exception occurred while generating explain plan: {0}", exception);
			}
		}

		private void GenerateSegmentMetrics([NotNull] Segment segment, TransactionMetricStatsCollection txStats)
		{ 
			segment.AddMetricStats(txStats, _configurationService);
		}

		private void GetApdexMetrics([NotNull] ImmutableTransaction immutableTransaction, Boolean isErrorTranasction, TimeSpan apdexT, [NotNull] String transactionApdexMetricName, TransactionMetricStatsCollection txStats)
		{
			var isWebTransaction = immutableTransaction.IsWebTransaction();

			if (isErrorTranasction)
			{
				MetricBuilder.TryBuildFrustratedApdexMetrics(isWebTransaction, transactionApdexMetricName, txStats);
			} else {
				MetricBuilder.TryBuildApdexMetrics(transactionApdexMetricName, isWebTransaction, immutableTransaction.Duration, apdexT, txStats);
			}
		}

		private void AddSqlTraceStats(SqlTraceStatsCollection txSqlTrStats, SqlTraceWireModel model)
		{
			txSqlTrStats.Insert(model);
		}

		private void ComputeSampled(ITransactionMetadata metadata)
		{
			if (_configurationService.Configuration.DistributedTracingEnabled)
			{
				metadata.SetSampled(_transactionEventAggregator.AdaptiveSampler);
			}
		}
	}
}