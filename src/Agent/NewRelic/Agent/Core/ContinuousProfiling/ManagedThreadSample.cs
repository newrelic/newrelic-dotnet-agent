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

    /// <summary>
    /// True when the native sampler observed this thread inside agent-owned background dispatch (a
    /// Scheduler-invoked timer callback) at the instant of capture -- a thread-IDENTITY signal, set
    /// regardless of what frames are on the stack. Catches agent threads parked in
    /// System.Threading.Monitor.Wait that no frame-text predicate can see (follow-up #16). False for
    /// batches captured before the v3 wire format (see BufferParser).
    /// </summary>
    public bool IsAgentWork { get; }

    public ManagedThreadSample(string threadName, long osThreadId, long traceIdHigh, long traceIdLow, long spanId, IReadOnlyList<string> frames, bool onCpu, bool isAgentWork = false)
    {
        ThreadName = threadName;
        OsThreadId = osThreadId;
        TraceIdHigh = traceIdHigh;
        TraceIdLow = traceIdLow;
        SpanId = spanId;
        Frames = frames;
        OnCpu = onCpu;
        IsAgentWork = isAgentWork;
    }
}
