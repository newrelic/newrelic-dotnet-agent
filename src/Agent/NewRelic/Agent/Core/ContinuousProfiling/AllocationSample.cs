// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.ContinuousProfiling;

public class AllocationSample
{
    public string ThreadName { get; }
    public long OsThreadId { get; }
    public long TraceIdHigh { get; }
    public long TraceIdLow { get; }
    public long SpanId { get; }
    public long TimestampMillis { get; }
    public ulong AllocatedSize { get; }
    public string TypeName { get; }
    public IReadOnlyList<string> Frames { get; } // leaf-first

    public AllocationSample(string threadName, long osThreadId, long traceIdHigh, long traceIdLow, long spanId,
        long timestampMillis, ulong allocatedSize, string typeName, IReadOnlyList<string> frames)
    {
        ThreadName = threadName;
        OsThreadId = osThreadId;
        TraceIdHigh = traceIdHigh;
        TraceIdLow = traceIdLow;
        SpanId = spanId;
        TimestampMillis = timestampMillis;
        AllocatedSize = allocatedSize;
        TypeName = typeName;
        Frames = frames;
    }
}
