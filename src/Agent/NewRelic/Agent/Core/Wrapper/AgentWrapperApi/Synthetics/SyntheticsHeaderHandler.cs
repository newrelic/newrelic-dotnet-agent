﻿using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics
{
    public interface ISyntheticsHeaderHandler
	{
        [NotNull]
        IEnumerable<KeyValuePair<String, String>> TryGetOutboundSyntheticsRequestHeader([NotNull] ITransaction transaction);

        [CanBeNull]
        SyntheticsHeader TryDecodeInboundRequestHeaders([NotNull] IDictionary<String, String> headers);
    }

	public class SyntheticsHeaderHandler : ISyntheticsHeaderHandler
	{
		[NotNull] private readonly IConfigurationService _configurationService;

		public SyntheticsHeaderHandler([NotNull] IConfigurationService configurationService)
		{
			_configurationService = configurationService;
		}

		public IEnumerable<KeyValuePair<String, String>> TryGetOutboundSyntheticsRequestHeader(ITransaction transaction)
		{
			var metadata = transaction.TransactionMetadata;

			if (!metadata.IsSynthetics)
				return Enumerable.Empty<KeyValuePair<String, String>>();

			Int64 accountId;
			if (!Int64.TryParse(_configurationService.Configuration.CrossApplicationTracingCrossProcessId.Split('#').FirstOrDefault(), out accountId))
				return Enumerable.Empty<KeyValuePair<String, String>>();

			var syntheticsHeader = new SyntheticsHeader(SyntheticsHeader.SupportedHeaderVersion, accountId, metadata.SyntheticsResourceId, metadata.SyntheticsJobId, metadata.SyntheticsMonitorId) {EncodingKey = _configurationService.Configuration.EncodingKey};

			var obfuscatedHeader = syntheticsHeader.TryGetObfuscated();
			if (obfuscatedHeader == null)
				return Enumerable.Empty<KeyValuePair<String, String>>();

			return new[]
			{
				new KeyValuePair<String, String>(SyntheticsHeader.HeaderKey, obfuscatedHeader)
			};
		}

		public SyntheticsHeader TryDecodeInboundRequestHeaders(IDictionary<String, String> headers)
		{
			var syntheticsDataHttpHeader = headers.GetValueOrDefault(SyntheticsHeader.HeaderKey);

			if (syntheticsDataHttpHeader == null)
				return null;

			return SyntheticsHeader.TryCreate(_configurationService.Configuration.TrustedAccountIds, syntheticsDataHttpHeader, _configurationService.Configuration.EncodingKey);
		}
	}
}
