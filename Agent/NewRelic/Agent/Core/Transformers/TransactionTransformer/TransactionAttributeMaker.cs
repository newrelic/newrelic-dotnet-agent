using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions;
using Attribute = NewRelic.Agent.Core.Transactions.Attribute;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ITransactionAttributeMaker
	{
		[NotNull]
		Attributes GetAttributes([NotNull] ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, [CanBeNull] TimeSpan? apdexT, TimeSpan totalTime, ErrorData errorData, TransactionMetricStatsCollection txStats);

		[NotNull]
		Attributes GetUserAndAgentAttributes([NotNull] ITransactionAttributeMetadata metadata);
	}

	public class TransactionAttributeMaker : ITransactionAttributeMaker
	{
		[NotNull] private readonly IConfigurationService _configurationService;
		public TransactionAttributeMaker([NotNull] IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}
		public Attributes GetAttributes(ImmutableTransaction immutableTransaction, TransactionMetricName transactionMetricName, TimeSpan? apdexT, TimeSpan totalTime, ErrorData errorData, TransactionMetricStatsCollection txStats)
		{

			var attributes = GetUserAndAgentAttributes(immutableTransaction.TransactionMetadata);

			// Required transaction attributes
			attributes.Add(Attribute.BuildTypeAttribute(TypeAttributeValue.Transaction));

			if (errorData.IsAnError)
			{
				attributes.Add(Attribute.BuildTimeStampAttribute(errorData.NoticedAt));
			}
			else
			{
				attributes.Add(Attribute.BuildTimeStampAttribute(immutableTransaction.StartTime));
			}

			attributes.Add(Attribute.BuildTransactionNameAttribute(transactionMetricName.PrefixedName));

			// Duration (response time) is just EndTime minus StartTime
			attributes.Add(Attribute.BuildDurationAttribute(immutableTransaction.Duration));

			// Total time is the total amount of time spent, even when work is happening parallel, which means it is the sum of all exclusive times.
			// https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
			attributes.Add(Attribute.BuildTotalTime(totalTime));

			// CPU time is the total time spent actually doing work rather than waiting. Basically, it's TotalTime minus TimeSpentWaiting.
			// Our agent does not yet the ability to calculate time spent waiting, so we cannot generate this metric.
			// https://source.datanerd.us/agents/agent-specs/blob/master/Total-Time-Async.md
			//attributes.Add(Attribute.BuildCpuTime(immutableTransaction.Duration));

			// Optional transaction attributes
			attributes.TryAdd(Attribute.BuildQueueDurationAttribute, immutableTransaction.TransactionMetadata.QueueTime);
			attributes.TryAdd(Attribute.BuildApdexPerfZoneAttribute, ApdexStats.GetApdexPerfZoneOrNull(immutableTransaction.Duration, apdexT));


			if (immutableTransaction.IsWebTransaction())
			{
				attributes.TryAdd(Attribute.BuildWebDurationAttribute, immutableTransaction.Duration);
			}

			var externalData = txStats.GetUnscopedStat(MetricNames.ExternalAll);
			if (externalData != null)
			{
				attributes.TryAdd(Attribute.BuildExternalDurationAttribute, externalData.Value1);
				attributes.TryAdd(Attribute.BuildExternalCallCountAttribute, (Single)externalData.Value0);
			}

			var databaseData = txStats.GetUnscopedStat(MetricNames.DatastoreAll);
			if (databaseData != null)
			{
				attributes.TryAdd(Attribute.BuildDatabaseDurationAttribute, databaseData.Value1);
				attributes.TryAdd(Attribute.BuildDatabaseCallCountAttribute, (Single)databaseData.Value0);
			}

			if (errorData.IsAnError)
			{
				attributes.TryAdd(Attribute.BuildErrorClassAttribute, errorData.ErrorTypeName);
				attributes.TryAdd(Attribute.BuildErrorTypeAttribute, errorData.ErrorTypeName);

				attributes.TryAdd(Attribute.BuildErrorMessageAttribute, errorData.ErrorMessage);
				attributes.TryAdd(Attribute.BuildErrorDotMessageAttribute, errorData.ErrorMessage);

				attributes.TryAdd(Attribute.BuildErrorAttribute, true);
			}

			var isCatParticipant = IsCatParticipant(immutableTransaction);
			var isSyntheticsParticipant = IsSyntheticsParticipant(immutableTransaction);
			var isDistributedTraceParticipant = immutableTransaction.TransactionMetadata.HasIncomingDistributedTracePayload;

			// add the tripId attribute unconditionally so it can be used to correlate with this app's PageView events
			// if CrossApplicationReferrerTripId is null then this transaction started the first external request, so use its guid
			var tripId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerTripId ?? immutableTransaction.Guid;
			attributes.TryAdd(Attribute.BuildTripUnderscoreIdAttribute, tripId);
			
			// nr.tripid is set differently if this is a distributed trace transaction
			if (_configurationService.Configuration.DistributedTracingEnabled == false)
			{
				attributes.TryAdd(Attribute.BuildCatNrTripIdAttribute, tripId);
			}

			if (isCatParticipant)
			{
				attributes.TryAdd(Attribute.BuildNrGuidAttribute, immutableTransaction.Guid);
				attributes.TryAddAll(Attribute.BuildCatReferringPathHash, immutableTransaction.TransactionMetadata.CrossApplicationReferrerPathHash);
				attributes.TryAddAll(Attribute.BuildCatPathHash, immutableTransaction.TransactionMetadata.CrossApplicationPathHash);
				attributes.TryAdd(Attribute.BuildClientCrossProcessIdAttribute, immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId);

				attributes.TryAddAll(Attribute.BuildCatReferringTransactionGuidAttribute, immutableTransaction.TransactionMetadata.CrossApplicationReferrerTransactionGuid);
				if (immutableTransaction.TransactionMetadata.CrossApplicationAlternatePathHashes.Any())
				{
					var hashes = String.Join(",", immutableTransaction.TransactionMetadata.CrossApplicationAlternatePathHashes.OrderBy(x => x).ToArray());
					attributes.TryAddAll(Attribute.BuildCatAlternatePathHashes, hashes);
				}
			}
			else if (isDistributedTraceParticipant)
			{
				//Won't be a DT participant if DT not enabled.
				attributes.TryAdd(Attribute.BuildParentTypeAttribute, immutableTransaction.TransactionMetadata.DistributedTraceType);
				attributes.TryAdd(Attribute.BuildParentAppAttribute, immutableTransaction.TransactionMetadata.DistributedTraceAppId);
				attributes.TryAdd(Attribute.BuildParentAccountAttribute, immutableTransaction.TransactionMetadata.DistributedTraceAccountId);
				attributes.TryAdd(Attribute.BuildParentTransportTypeAttribute, immutableTransaction.TransactionMetadata.DistributedTraceTransportType);
				attributes.TryAdd(Attribute.BuildParentTransportDurationAttribute, immutableTransaction.TransactionMetadata.DistributedTraceTransportDuration);
				attributes.TryAdd(Attribute.BuildParentIdAttribute, immutableTransaction.TransactionMetadata.DistributedTraceTransactionId);
				attributes.TryAdd(Attribute.BuildParentSpanIdAttribute,immutableTransaction.TransactionMetadata.DistributedTraceGuid);
			}

			if (_configurationService.Configuration.DistributedTracingEnabled)
			{
				attributes.TryAdd(Attribute.BuildGuidAttribute, immutableTransaction.Guid);
				attributes.TryAdd(Attribute.BuildDistributedTraceIdAttributes, immutableTransaction.TransactionMetadata.DistributedTraceTraceId ?? immutableTransaction.Guid);
				attributes.TryAdd(Attribute.BuildPriorityAttribute, immutableTransaction.TransactionMetadata.Priority);
				attributes.TryAdd(Attribute.BuildSampledAttribute, immutableTransaction.TransactionMetadata.DistributedTraceSampled);
			}

			if (isSyntheticsParticipant)
			{
				attributes.TryAdd(Attribute.BuildNrGuidAttribute, immutableTransaction.Guid);
				attributes.TryAddAll(Attribute.BuildSyntheticsResourceIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsResourceId);
				attributes.TryAddAll(Attribute.BuildSyntheticsJobIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsJobId);
				attributes.TryAddAll(Attribute.BuildSyntheticsMonitorIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsMonitorId);
			}

			return attributes;
		}
		
		public Attributes GetUserAndAgentAttributes(ITransactionAttributeMetadata metadata)
		{
			var attributes = new Attributes();

			attributes.TryAdd(Attribute.BuildRequestUriAttribute, metadata.Uri ?? "/Unknown");

			// original_url should only be generated if it is distinct from the current URI
			if (metadata.OriginalUri != metadata.Uri)
				attributes.TryAdd(Attribute.BuildOriginalUrlAttribute, metadata.OriginalUri);

			attributes.TryAdd(Attribute.BuildRequestRefererAttribute, metadata.ReferrerUri);
			attributes.TryAdd(Attribute.BuildQueueWaitTimeAttribute, metadata.QueueTime);
			attributes.TryAdd(Attribute.BuildResponseStatusAttribute, metadata.HttpResponseStatusCode?.ToString());

			foreach(var reqParam in metadata.RequestParameters)
			{
				var rpa = Attribute.BuildRequestParameterAttribute(reqParam.Key, reqParam.Value);
				attributes.Add(rpa);
			}

			foreach(var userAttr in metadata.UserAttributes)
			{
				var ca = Attribute.BuildCustomAttribute(userAttr.Key, userAttr.Value);
				attributes.Add(ca);
			}

			//hmm pretty sure this is wrong - error attributes only go on errors
			foreach(var errAttr in metadata.UserErrorAttributes)
			{
				var cea = Attribute.BuildCustomErrorAttribute(errAttr.Key, errAttr.Value);
				attributes.Add(cea);
			}

			return attributes;
		}

		private static Boolean IsCatParticipant([NotNull] ImmutableTransaction immutableTransaction)
		{
			// The logic of this method is specced in a footnote here: https://source.datanerd.us/agents/agent-specs/blob/master/Cross-Application-Tracing-PORTED.md#attributes
			// In short, you are a CAT participant if you received valid CAT headers on an inbound request data or you received an inbound response with CAT data

			if (immutableTransaction.TransactionMetadata.CrossApplicationReferrerProcessId != null)
				return true;
			// check if any segment has cross application response data according to the comment above and previous code
			return (immutableTransaction.TransactionMetadata.HasCatResponseHeaders);
		}

		private static Boolean IsSyntheticsParticipant([NotNull] ImmutableTransaction immutableTransaction)
		{
			return (immutableTransaction.TransactionMetadata.SyntheticsResourceId != null && immutableTransaction.TransactionMetadata.SyntheticsJobId != null && immutableTransaction.TransactionMetadata.SyntheticsMonitorId != null);

		}
	}
}
