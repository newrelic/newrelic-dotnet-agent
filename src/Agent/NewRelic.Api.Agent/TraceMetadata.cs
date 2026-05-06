// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

internal class TraceMetadata : ITraceMetadata
{
    private static ITraceMetadata _noOpTraceMetadata = new NoOpTraceMetadata();
    private dynamic _wrappedTraceMetadata = _noOpTraceMetadata;

    internal TraceMetadata(dynamic wrappedTraceMetadata)
    {
        _wrappedTraceMetadata = wrappedTraceMetadata;
    }

    public string TraceId
    {
        get
        {
            return _wrappedTraceMetadata.TraceId;
        }
    }

    public string SpanId
    {
        get
        {
            return _wrappedTraceMetadata.SpanId;
        }
    }

    public bool IsSampled
    {
        get
        {
            return _wrappedTraceMetadata.IsSampled;
        }
    }
}