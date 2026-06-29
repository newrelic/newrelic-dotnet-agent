// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using NewRelic.Agent.Core.Segments;

namespace NewRelic.Agent.Core.DataTransport;

public class SpanUnaryGrpcWrapper : GrpcUnaryWrapper<Span, RecordStatus>, IGrpcUnaryWrapper<Span, RecordStatus>
{
    protected override string MethodPath => "/com.newrelic.trace.v1.IngestService/RecordSpanUnary";
    protected override MessageParser<RecordStatus> ResponseParser => RecordStatus.Parser;
}

public class SpanBatchUnaryGrpcWrapper : GrpcUnaryWrapper<SpanBatch, RecordStatus>, IGrpcUnaryWrapper<SpanBatch, RecordStatus>
{
    protected override string MethodPath => "/com.newrelic.trace.v1.IngestService/RecordSpanBatchUnary";
    protected override MessageParser<RecordStatus> ResponseParser => RecordStatus.Parser;
}
