// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Segments;
using Grpc.Core;
using System.Threading;

namespace NewRelic.Agent.Core.DataTransport
{
    public class SpanGrpcWrapper : GrpcWrapper<Span, RecordStatus>, IGrpcWrapper<Span, RecordStatus>
    {
        protected override AsyncDuplexStreamingCall<Span, RecordStatus> CreateStreamsImpl(Channel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            if (channel == null)
            {
                throw new GrpcWrapperChannelNotAvailableException();
            }

            if (!channel.ConnectAsync().Wait(connectTimeoutMs, cancellationToken))
            {
                // Esure channel connection attempt shutdown on timeout
                channel.ShutdownAsync().Wait();

                throw new GrpcWrapperChannelNotAvailableException();
            }

            var client = new IngestService.IngestServiceClient(channel);
            var streams = client.RecordSpan(headers: headers, cancellationToken: cancellationToken);

            return streams;
        }
    }

    public class SpanBatchGrpcWrapper : GrpcWrapper<SpanBatch, RecordStatus>, IGrpcWrapper<SpanBatch, RecordStatus>
    {
        protected override AsyncDuplexStreamingCall<SpanBatch, RecordStatus> CreateStreamsImpl(Channel channel, Metadata headers, int connectTimeoutMs, CancellationToken cancellationToken)
        {
            if (channel == null)
            {
                throw new GrpcWrapperChannelNotAvailableException();
            }

            if (!channel.ConnectAsync().Wait(connectTimeoutMs, cancellationToken))
            {
                // Esure channel connection attempt shutdown on timeout
                channel.ShutdownAsync().Wait();

                throw new GrpcWrapperChannelNotAvailableException();
            }

            var client = new IngestService.IngestServiceClient(channel);
            var streams = client.RecordSpanBatch(headers: headers, cancellationToken: cancellationToken);

            return streams;
        }
    }

}
