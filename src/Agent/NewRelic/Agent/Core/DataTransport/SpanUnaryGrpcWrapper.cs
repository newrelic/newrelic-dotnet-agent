// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using Grpc.Core;
using Grpc.Net.Client;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.DataTransport;

public class SpanUnaryGrpcWrapper : GrpcUnaryWrapper<Span, RecordStatus>, IGrpcUnaryWrapper<Span, RecordStatus>
{
    protected override RecordStatus SendDataImpl(GrpcChannel channel, Span item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken)
    {
        if (channel == null)
        {
            throw new GrpcWrapperChannelNotAvailableException();
        }

        var client = new IngestService.IngestServiceClient(channel);
        var deadline = DateTime.UtcNow.AddMilliseconds(sendTimeoutMs);

        return client.RecordSpanUnary(item, headers: headers, deadline: deadline, cancellationToken: cancellationToken);
    }
}

public class SpanBatchUnaryGrpcWrapper : GrpcUnaryWrapper<SpanBatch, RecordStatus>, IGrpcUnaryWrapper<SpanBatch, RecordStatus>
{
    protected override RecordStatus SendDataImpl(GrpcChannel channel, SpanBatch item, Metadata headers, int sendTimeoutMs, CancellationToken cancellationToken)
    {
        if (channel == null)
        {
            throw new GrpcWrapperChannelNotAvailableException();
        }

        var client = new IngestService.IngestServiceClient(channel);
        var deadline = DateTime.UtcNow.AddMilliseconds(sendTimeoutMs);

        return client.RecordSpanBatchUnary(item, headers: headers, deadline: deadline, cancellationToken: cancellationToken);
    }
}
