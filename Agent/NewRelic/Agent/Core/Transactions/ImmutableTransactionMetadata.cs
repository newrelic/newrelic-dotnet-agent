using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Transactions
{
	public class ImmutableTransactionMetadata : ITransactionAttributeMetadata
	{
		[NotNull]
		public IEnumerable<KeyValuePair<string, string>> RequestParameters { get; }

		[NotNull]
		public IEnumerable<KeyValuePair<string, Object>> UserAttributes { get; }

		[NotNull]
		public IEnumerable<KeyValuePair<string, Object>> UserErrorAttributes { get; }

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
		public int? HttpResponseSubStatusCode { get; }

		public string SyntheticsResourceId { get; }
		public string SyntheticsJobId { get; }
		public string SyntheticsMonitorId { get; }
		public bool IsSynthetics { get; }
		public bool HasCatResponseHeaders { get; }
		public float Priority { get; }

		public ImmutableTransactionMetadata(string uri, string originalUri, string referrerUri,
			TimeSpan? queueTime, [NotNull] IEnumerable<KeyValuePair<string, string>> requestParameters,
			[NotNull] IEnumerable<KeyValuePair<string, Object>> userAttributes,
			[NotNull] IEnumerable<KeyValuePair<string, Object>> userErrorAttributes, int? httpResponseStatusCode,
			Int32? httpResponseSubStatusCode, [NotNull] IEnumerable<ErrorData> transactionExceptionDatas,
			[NotNull] IEnumerable<ErrorData> customErrorDatas, string crossApplicationReferrerPathHash, string crossApplicationPathHash,
			[NotNull] IEnumerable<string> crossApplicationPathHashes, string crossApplicationReferrerTransactionGuid,
			string crossApplicationReferrerProcessId, string crossApplicationReferrerTripId, string syntheticsResourceId,
			string syntheticsJobId, string syntheticsMonitorId, bool isSynthetics, bool hasCatResponseHeaders, float priority)
		{
			Uri = uri;
			OriginalUri = originalUri;
			ReferrerUri = referrerUri;
			QueueTime = queueTime;
			RequestParameters = requestParameters.ToList();
			UserAttributes = userAttributes.ToList();
			UserErrorAttributes = userErrorAttributes.ToList();
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
			SyntheticsResourceId = syntheticsResourceId;
			SyntheticsJobId = syntheticsJobId;
			SyntheticsMonitorId = syntheticsMonitorId;
			IsSynthetics = isSynthetics;
			HasCatResponseHeaders = hasCatResponseHeaders;
			Priority = priority;
		}
	}
}