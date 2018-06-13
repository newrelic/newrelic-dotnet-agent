using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Transactions
{
	public class ImmutableTransactionMetadata : ITransactionAttributeMetadata
	{
		public KeyValuePair<string, string>[] RequestParameters { get; }
		public KeyValuePair<string, object>[] UserAttributes { get; }
		public KeyValuePair<string, object>[] UserErrorAttributes { get; }

		public string Uri { get; }
		public string OriginalUri { get; }
		public string ReferrerUri { get; }
		public TimeSpan? QueueTime { get; }
		public int? HttpResponseStatusCode { get; }

		[NotNull]
		public IEnumerable<ErrorData> TransactionExceptionDatas { get; }

		[NotNull]
		public IEnumerable<ErrorData> CustomErrorDatas { get; }

		[NotNull]
		public IEnumerable<string> CrossApplicationAlternatePathHashes { get; }

		[CanBeNull]
		public string CrossApplicationReferrerTransactionGuid { get; }

		[CanBeNull]
		public string CrossApplicationReferrerPathHash { get; }

		[CanBeNull]
		public string CrossApplicationPathHash { get; }

		public string CrossApplicationReferrerProcessId { get; }
		public string CrossApplicationReferrerTripId { get; }

		public string DistributedTraceParentType { get; set; }
		public string DistributedTraceParentId { get; set; }
		public string DistributedTraceTraceId { get; set; }
		public bool DistributedTraceSampled { get; set; }

		public int? HttpResponseSubStatusCode { get; }

		public string SyntheticsResourceId { get; }
		public string SyntheticsJobId { get; }
		public string SyntheticsMonitorId { get; }
		public bool IsSynthetics { get; }
		public bool HasCatResponseHeaders { get; }
		public float Priority { get; }

		public ImmutableTransactionMetadata(
			string uri,
			string originalUri,
			string referrerUri,
			TimeSpan? queueTime,
			ConcurrentDictionary<string, string> requestParameters,
			ConcurrentDictionary<string, object> userAttributes,
			ConcurrentDictionary<string, object> userErrorAttributes,
			int? httpResponseStatusCode,
			int? httpResponseSubStatusCode,
			IEnumerable<ErrorData> transactionExceptionDatas,
			IEnumerable<ErrorData> customErrorDatas,
			string crossApplicationReferrerPathHash,
			string crossApplicationPathHash,
			IEnumerable<string> crossApplicationPathHashes,
			string crossApplicationReferrerTransactionGuid,
			string crossApplicationReferrerProcessId,
			string crossApplicationReferrerTripId,
			string distributedTraceParentType,
			string distributedTraceParentId,
			string distributedTraceTraceId,
			bool distributedTraceSampled,
			string syntheticsResourceId,
			string syntheticsJobId,
			string syntheticsMonitorId,
			bool isSynthetics,
			bool hasCatResponseHeaders,
			float priority)
		{
			Uri = uri;
			OriginalUri = originalUri;
			ReferrerUri = referrerUri;
			QueueTime = queueTime;

			// The following must use ToArray because ToArray is thread safe on a ConcurrentDictionary.
			RequestParameters = requestParameters.ToArray();
			UserAttributes = userAttributes.ToArray();
			UserErrorAttributes = userErrorAttributes.ToArray();

			HttpResponseStatusCode = httpResponseStatusCode;
			HttpResponseSubStatusCode = httpResponseSubStatusCode;
			TransactionExceptionDatas = transactionExceptionDatas;
			CustomErrorDatas = customErrorDatas;
			CrossApplicationReferrerPathHash = crossApplicationReferrerPathHash;
			CrossApplicationPathHash = crossApplicationPathHash;
			CrossApplicationAlternatePathHashes = crossApplicationPathHashes.ToList();
			CrossApplicationReferrerTransactionGuid = crossApplicationReferrerTransactionGuid;
			CrossApplicationReferrerProcessId = crossApplicationReferrerProcessId;
			CrossApplicationReferrerTripId = crossApplicationReferrerTripId;
			DistributedTraceParentType = distributedTraceParentType;
			DistributedTraceParentId = distributedTraceParentId;
			DistributedTraceTraceId = distributedTraceTraceId;
			DistributedTraceSampled = distributedTraceSampled;
			SyntheticsResourceId = syntheticsResourceId;
			SyntheticsJobId = syntheticsJobId;
			SyntheticsMonitorId = syntheticsMonitorId;
			IsSynthetics = isSynthetics;
			HasCatResponseHeaders = hasCatResponseHeaders;
			Priority = priority;
		}
	}
}