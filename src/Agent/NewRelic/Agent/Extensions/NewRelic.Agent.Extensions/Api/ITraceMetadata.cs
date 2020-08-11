// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Api
{
    public interface ITraceMetadata
    {
        string TraceId { get; }

        string SpanId { get; }

        bool IsSampled { get; }
    }
}
