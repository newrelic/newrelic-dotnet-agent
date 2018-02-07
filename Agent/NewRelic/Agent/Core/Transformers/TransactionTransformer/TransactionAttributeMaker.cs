using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions;
using NewRelic.SystemExtensions.Collections.Generic;
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
			}

			var isCatParticipant = IsCatParticipant(immutableTransaction);
			var isSyntheticsParticipant = IsSyntheticsParticipant(immutableTransaction);

			// Add the GUID attribute if we are dealing with CAT or synthetics
			if (isCatParticipant || isSyntheticsParticipant)
			{
				attributes.TryAdd(Attribute.BuildGuidAttribute, immutableTransaction.Guid);
			}

			// add the tripId attribute unconditionally so it can be used to correlate with this app's PageView events
			// if CrossApplicationReferrerTripId is null then this transaction started the first external request, so use its guid
			var tripId = immutableTransaction.TransactionMetadata.CrossApplicationReferrerTripId ?? immutableTransaction.Guid;
			attributes.TryAddAll(Attribute.BuildCatTripIdAttribute, tripId);

			if (isCatParticipant)
			{
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

			if (isSyntheticsParticipant)
			{
				attributes.TryAddAll(Attribute.BuildSyntheticsResourceIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsResourceId);
				attributes.TryAddAll(Attribute.BuildSyntheticsJobIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsJobId);
				attributes.TryAddAll(Attribute.BuildSyntheticsMonitorIdAttributes, immutableTransaction.TransactionMetadata.SyntheticsMonitorId);
			}
			
			return attributes;
		}
		
		public Attributes GetUserAndAgentAttributes(ITransactionAttributeMetadata metadata)
		{
			var attributes = new Attributes();

			attributes.TryAdd(Attribute.BuildRequestUriAttribute, metadata.Uri);

			// original_url should only be generated if it is distinct from the current URI
			if (metadata.OriginalUri != metadata.Uri)
				attributes.TryAdd(Attribute.BuildOriginalUrlAttribute, metadata.OriginalUri);

			attributes.TryAdd(Attribute.BuildRequestRefererAttribute, metadata.ReferrerUri);
			attributes.TryAdd(Attribute.BuildQueueWaitTimeAttribute, metadata.QueueTime);
			attributes.TryAdd(Attribute.BuildResponseStatusAttribute, metadata.HttpResponseStatusCode?.ToString());

			metadata.RequestParameters
				.Select(param => Attribute.BuildRequestParameterAttribute(param.Key, param.Value))
				.ForEach(attributes.Add);
			metadata.ServiceParameters
				.Select(param => Attribute.BuildServiceRequestAttribute(param.Key, param.Value))
				.ForEach(attributes.Add);
			metadata.UserAttributes
				.Select(param => Attribute.BuildCustomAttribute(param.Key, param.Value))
				.ForEach(attributes.Add);
			//hmm pretty sure this is wrong - error attributes only go on errors
			metadata.UserErrorAttributes
				.Select(param => Attribute.BuildCustomErrorAttribute(param.Key, param.Value))
				.ForEach(attributes.Add);

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

		[NotNull]
		private static IEnumerable<ImmutableSegmentTreeNode> GetNodesOfType<T>([NotNull] IEnumerable<ImmutableSegmentTreeNode> roots)
		{
			return roots
				.SelectMany(root => root.Flatten(node => node.Children))
				.Where(node => node != null)
				.Where(node => node.Segment.GetType() == typeof (T));
		}
	}
}
