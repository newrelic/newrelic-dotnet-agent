/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
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
                throw new GrpcWrapperChannelNotAvailableException();
            }

            var client = new IngestService.IngestServiceClient(channel);
            var streams = client.RecordSpan(headers: headers, cancellationToken: cancellationToken);

            return streams;
        }
    }

}
