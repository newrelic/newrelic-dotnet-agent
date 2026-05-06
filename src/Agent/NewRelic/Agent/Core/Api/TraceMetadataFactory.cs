// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Api;

public class TraceMetadata : ITraceMetadata
{
    public static readonly ITraceMetadata EmptyModel = new TraceMetadata(string.Empty, string.Empty, false);

    public string TraceId { get; private set; }

    public string SpanId { get; private set; }

    public bool IsSampled { get; private set; }

    public TraceMetadata(string traceId, string spanId, bool isSampled)
    {
        TraceId = traceId;
        SpanId = spanId;
        IsSampled = isSampled;
    }
}

public interface ITraceMetadataFactory
{
    ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction);
}

public class TraceMetadataFactory : ITraceMetadataFactory
{
    public ITraceMetadata CreateTraceMetadata(IInternalTransaction transaction)
    {
        var traceId = transaction.TraceId;
        var spanId = transaction.CurrentSegment.SpanId;

        // if Sampled has not been set, compute it now
        if (transaction.Sampled is null)
        {
            transaction.SetSampled();
        }

        var isSampled = transaction.Sampled ?? false;

        return new TraceMetadata(traceId, spanId, isSampled);
    }
}
