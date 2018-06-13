using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.DistributedTracing
{
	public interface IDistributedTracePayloadHandler
	{
		[NotNull]
		IEnumerable<KeyValuePair<String, String>> TryGetOutboundRequestHeaders([NotNull] ITransaction transaction);

		[CanBeNull]
		DistributedTracePayload TryDecodeInboundRequestHeaders([NotNull] IDictionary<String, String> headers);
	}

	public class DistributedTracePayloadHandler : IDistributedTracePayloadHandler
	{
		private const String DistributedTraceHeaderName = "X-NewRelic-Trace";   // TODO: tracenewrelic Tracenewrelic TRACENEWRELIC Trace-Parent and Trace-State?


		[NotNull]
		private readonly IConfigurationService _configurationService;

		public DistributedTracePayloadHandler([NotNull] IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}

		public IEnumerable<KeyValuePair<String, String>> TryGetOutboundRequestHeaders(ITransaction transaction)
		{
			// TODO: implement this
			return Enumerable.Empty<KeyValuePair<String, String>>();
		}

		public DistributedTracePayload TryDecodeInboundRequestHeaders(IDictionary<String, String> headers)
		{
			var distributedTracePairs = headers.GetValueOrDefault(DistributedTraceHeaderName);
			if (distributedTracePairs == null)
				return null;

			// TODO: validation stuff re:accountId is trusted, etc.

			return HeaderEncoder.TryDecodeAndDeserializeDistributedTracePayload<DistributedTracePayload>(distributedTracePairs, _configurationService.Configuration.EncodingKey);
		}

		private Boolean IsTrustedCrossProcessAccountId(String accountId, IEnumerable<Int64> trustedAccountIds)
		{
			Int64 requestAccountId;
			if (!Int64.TryParse(accountId.Split('#').FirstOrDefault(), out requestAccountId))
				return false;
			if (!trustedAccountIds.Contains(requestAccountId))
				return false;
			return true;
		}
	}
}
