// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Synthetics;

public interface ISyntheticsHeaderHandler
{
    IEnumerable<KeyValuePair<string, string>> TryGetOutboundSyntheticsRequestHeader(IInternalTransaction transaction);
    SyntheticsHeader TryDecodeInboundRequestHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter);
}

public class SyntheticsHeaderHandler : ISyntheticsHeaderHandler
{
    private readonly IConfigurationService _configurationService;

    public SyntheticsHeaderHandler(IConfigurationService configurationService)
    {
        _configurationService = configurationService;
    }

    public IEnumerable<KeyValuePair<string, string>> TryGetOutboundSyntheticsRequestHeader(IInternalTransaction transaction)
    {
        var metadata = transaction.TransactionMetadata;

        if (!metadata.IsSynthetics)
            return Enumerable.Empty<KeyValuePair<string, string>>();

        long accountId;
        if (!long.TryParse(_configurationService.Configuration.CrossApplicationTracingCrossProcessId.Split(StringSeparators.Hash)[0], out accountId))
            return Enumerable.Empty<KeyValuePair<string, string>>();

        var syntheticsHeader = new SyntheticsHeader(SyntheticsHeader.SupportedHeaderVersion, accountId, metadata.SyntheticsResourceId, metadata.SyntheticsJobId, metadata.SyntheticsMonitorId) { EncodingKey = _configurationService.Configuration.EncodingKey };

        var obfuscatedHeader = syntheticsHeader.TryGetObfuscated();
        if (obfuscatedHeader == null)
            return Enumerable.Empty<KeyValuePair<string, string>>();

        return new[]
        {
            new KeyValuePair<string, string>(SyntheticsHeader.HeaderKey, obfuscatedHeader)
        };
    }

    public SyntheticsHeader TryDecodeInboundRequestHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        var syntheticsDataHttpHeader = getter(carrier, SyntheticsHeader.HeaderKey)?.FirstOrDefault();

        if (syntheticsDataHttpHeader == null)
            return null;

        return SyntheticsHeader.TryCreate(_configurationService.Configuration.TrustedAccountIds, syntheticsDataHttpHeader, _configurationService.Configuration.EncodingKey);
    }
}
