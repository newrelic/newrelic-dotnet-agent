// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using Grpc.Core;
using NewRelic.Agent.Core.Segments;
#if LEGACY_GRPC
using GrpcChannel = Grpc.Core.Channel;
#else
using Grpc.Net.Client;
#endif

namespace NewRelic.Agent.Core.DataTransport;

public class SpanGrpcWrapper : GrpcWrapper<Span, RecordStatus>, IGrpcWrapper<Span, RecordStatus>
{
    protected override AsyncDuplexStreamingCall<Span, RecordStatus> CreateStreamsImpl(GrpcChannel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        if (channel == null)
        {
            throw new GrpcWrapperChannelNotAvailableException();
        }
#if LEGACY_GRPC
        if (!channel.ConnectAsync(DateTime.Now.AddMilliseconds(connectTimeoutMs)).Wait(connectTimeoutMs, cancellationToken))
        {
            // Ensure channel connection attempt shutdown on timeout
            channel.ShutdownAsync().Wait();

            throw new GrpcWrapperChannelNotAvailableException();
        }
#endif

        var client = new IngestService.IngestServiceClient(channel);
        var streams = client.RecordSpan(headers: headers, cancellationToken: cancellationToken);

        return streams;
    }
}

public class SpanBatchGrpcWrapper : GrpcWrapper<SpanBatch, RecordStatus>, IGrpcWrapper<SpanBatch, RecordStatus>
{
    protected override AsyncDuplexStreamingCall<SpanBatch, RecordStatus> CreateStreamsImpl(GrpcChannel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
    {
        if (channel == null)
        {
            throw new GrpcWrapperChannelNotAvailableException();
        }

#if LEGACY_GRPC
        if (!channel.ConnectAsync(DateTime.Now.AddMilliseconds(connectTimeoutMs)).Wait(connectTimeoutMs, cancellationToken))
        {
            // Ensure channel connection attempt shutdown on timeout
            channel.ShutdownAsync().Wait();

            throw new GrpcWrapperChannelNotAvailableException();
        }
#endif

        var client = new IngestService.IngestServiceClient(channel);
        var streams = client.RecordSpanBatch(headers: headers, cancellationToken: cancellationToken);

        return streams;
    }
}
