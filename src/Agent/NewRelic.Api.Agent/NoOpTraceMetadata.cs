// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

internal class NoOpTraceMetadata : ITraceMetadata
{
    public string TraceId => string.Empty;

    public string SpanId => string.Empty;

    public bool IsSampled => false;
}