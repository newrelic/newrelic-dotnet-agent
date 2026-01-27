// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.Transactions;

public class ImmutableTransaction
{
    public readonly ITransactionName TransactionName;

    public readonly IList<Segment> Segments;

    public readonly IImmutableTransactionMetadata TransactionMetadata;

    public readonly DateTime StartTime;
    public readonly TimeSpan Duration;
    public readonly TimeSpan ResponseTimeOrDuration;

    public readonly string Guid;

    public readonly bool IgnoreAutoBrowserMonitoring;
    public readonly bool IgnoreAllBrowserMonitoring;
    public readonly bool IgnoreApdex;
    public readonly float Priority;
    public readonly bool Sampled;
    public readonly string TraceId;
    public readonly ITracingState TracingState;

    private readonly IAttributeDefinitions _attribDefs;

    // The sqlObfuscator parameter should be the SQL obfuscator as defined by user configuration: obfuscate, off, or raw.
    public ImmutableTransaction(ITransactionName transactionName, IEnumerable<Segment> segments, IImmutableTransactionMetadata transactionMetadata, DateTime startTime, TimeSpan duration, TimeSpan? responseTime, string guid, bool ignoreAutoBrowserMonitoring, bool ignoreAllBrowserMonitoring, bool ignoreApdex, float priority, bool? sampled, string traceId, ITracingState tracingState, IAttributeDefinitions attribDefs)
    {
        TransactionName = transactionName;
        Segments = segments.Where(segment => segment != null).ToList();
        TransactionMetadata = transactionMetadata;
        StartTime = startTime;
        Duration = duration;
        ResponseTimeOrDuration = IsWebTransaction() ? responseTime ?? Duration : Duration;
        Guid = guid;
        IgnoreAutoBrowserMonitoring = ignoreAutoBrowserMonitoring;
        IgnoreAllBrowserMonitoring = ignoreAllBrowserMonitoring;
        IgnoreApdex = ignoreApdex;
        Priority = priority;
        Sampled = sampled.HasValue ? sampled.Value : false;
        TraceId = traceId;
        TracingState = tracingState;

        _attribDefs = attribDefs;
    }

    private SpanAttributeValueCollection _commonSpanAttributes;
    public SpanAttributeValueCollection CommonSpanAttributes
    {
        get
        {
            if (_commonSpanAttributes == null)
            {
                _commonSpanAttributes = new SpanAttributeValueCollection();

                _attribDefs.GetTypeAttribute(TypeAttributeValue.Span).TrySetDefault(_commonSpanAttributes);
                _attribDefs.DistributedTraceId.TrySetValue(_commonSpanAttributes, TraceId);
                _attribDefs.TransactionId.TrySetValue(_commonSpanAttributes, Guid);
                _attribDefs.Sampled.TrySetValue(_commonSpanAttributes, Sampled);
                _attribDefs.Priority.TrySetValue(_commonSpanAttributes, Priority);
            }

            return _commonSpanAttributes;
        }
    }

    public bool IsWebTransaction()
    {
        return TransactionName.IsWeb;
    }
}