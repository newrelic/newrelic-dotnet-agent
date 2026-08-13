// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.ContinuousProfiling;

public class ManagedThreadSample
{
    public string ThreadName { get; }
    public long OsThreadId { get; }
    public long TraceIdHigh { get; }
    public long TraceIdLow { get; }
    public long SpanId { get; }
    public IReadOnlyList<string> Frames { get; } // leaf-first
    public bool OnCpu { get; }

    public ManagedThreadSample(string threadName, long osThreadId, long traceIdHigh, long traceIdLow, long spanId, IReadOnlyList<string> frames, bool onCpu)
    {
        ThreadName = threadName;
        OsThreadId = osThreadId;
        TraceIdHigh = traceIdHigh;
        TraceIdLow = traceIdLow;
        SpanId = spanId;
        Frames = frames;
        OnCpu = onCpu;
    }
}
