// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// Base for the unary IngestService wrappers. Caches the generated gRPC client per channel so we
/// don't allocate a new client on every send. gRPC clients are thread-safe for concurrent calls,
/// so a single instance is shared across the worker threads. The cache self-invalidates when the
/// channel is recreated (the base creates a new channel on connect/restart and nulls it on shutdown).
/// </summary>
public abstract class IngestServiceUnaryGrpcWrapper<TRequest, TResponse> : GrpcUnaryWrapper<TRequest, TResponse>
{
    private sealed class ClientHolder
    {
        public readonly GrpcChannel Channel;
        public readonly IngestService.IngestServiceClient Client;

        public ClientHolder(GrpcChannel channel, IngestService.IngestServiceClient client)
        {
            Channel = channel;
            Client = client;
        }
    }

    // The (channel, client) pair lives in one immutable holder behind a single reference field, so a
    // reader always observes a consistent pair (a reference read/write is atomic on its own). The
    // holder is published with Volatile.Write and read with Volatile.Read: the release/acquire pair
    // guarantees the holder's readonly fields are visible to other worker threads on weak memory
    // models (e.g. arm64) - without the fences a reader could see the new reference but stale/null
    // fields. We use the Volatile class rather than a 'volatile' field per current .NET guidance
    // (clearer, explicit at each access site). A benign race at most builds one extra client on a
    // channel swap; gRPC clients are thread-safe for concurrent calls, so a single instance is shared.
    private ClientHolder _holder;

    protected IngestService.IngestServiceClient GetClient(GrpcChannel channel)
    {
        if (channel == null)
        {
            throw new GrpcWrapperChannelNotAvailableException();
        }

        var holder = Volatile.Read(ref _holder);
        if (holder == null || !ReferenceEquals(holder.Channel, channel))
        {
            holder = new ClientHolder(channel, new IngestService.IngestServiceClient(channel));
            Volatile.Write(ref _holder, holder);
        }

        return holder.Client;
    }
}

public class SpanUnaryGrpcWrapper : IngestServiceUnaryGrpcWrapper<Span, RecordStatus>, IGrpcUnaryWrapper<Span, RecordStatus>
{
    protected override RecordStatus SendDataImpl(GrpcChannel channel, Span item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken)
    {
        var client = GetClient(channel);
        var deadline = DateTime.UtcNow.AddMilliseconds(sendTimeoutMs);
        return client.RecordSpanUnary(item, headers: headers, deadline: deadline, cancellationToken: cancellationToken);
    }
}

public class SpanBatchUnaryGrpcWrapper : IngestServiceUnaryGrpcWrapper<SpanBatch, RecordStatus>, IGrpcUnaryWrapper<SpanBatch, RecordStatus>
{
    protected override RecordStatus SendDataImpl(GrpcChannel channel, SpanBatch item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken)
    {
        var client = GetClient(channel);
        var deadline = DateTime.UtcNow.AddMilliseconds(sendTimeoutMs);
        return client.RecordSpanBatchUnary(item, headers: headers, deadline: deadline, cancellationToken: cancellationToken);
    }
}
