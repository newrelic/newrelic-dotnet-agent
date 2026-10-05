// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core;

public interface INativeMethods
{
    void ReleaseProfile();
    int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);
    int RequestProfile([Out] out IntPtr snapshots, [Out] out int length);
    void ShutdownNativeThreadProfiler();

    int InstrumentationRefresh();
    int ReloadConfiguration();
    int AddCustomInstrumentation(string fileName, string xml);
    int ApplyCustomInstrumentation();

    // Every continuous-profiler entry point returns an int status (0 == success, non-zero == an
    // HRESULT-style failure), matching the other native-call families in this interface. Native
    // failures (profiler singleton not initialized, null ICorProfilerInfo, worker-thread creation
    // failure) reach managed code instead of vanishing. ContinuousProfilerReadThreadSamples keeps
    // its existing contract: the return is the byte count drained (<= 0 == nothing / error).
    int ContinuousProfilerStart(int intervalMs);
    int ContinuousProfilerStop();
    int ContinuousProfilerReadThreadSamples(int len, byte[] buffer);
    int ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId);
    int ContinuousProfilerResetTraceContext();

    /// <summary>
    /// Marks the first <paramref name="count"/> span ids in <paramref name="spanIds"/> as ended, so no
    /// later CPU sample can be linked to one. Called once per terminating transaction.
    /// </summary>
    int ContinuousProfilerRetireSpans(long[] spanIds, int count);

    /// <summary>
    /// Hands the CALLING thread the address of its native pending-push cell, where it publishes the span id
    /// it is about to push before starting the push so a compaction pass can see an intent no native slot
    /// reflects yet. Called ONCE per managed thread: the address is valid only until that thread exits, so it
    /// must never be cached anywhere outliving the thread. A non-zero status means "no cell" (E_POINTER for a
    /// null out-param, E_FAIL for an unallocated table / exhausted probe budget / unresolvable thread id),
    /// which is benign -- the caller pushes without publishing its intent.
    /// </summary>
    int ContinuousProfilerGetPendingPushCell([Out] out IntPtr cell);
    int ContinuousProfilerSetAgentWork();
    int ContinuousProfilerResetAgentWork();
    int ContinuousProfilerShutdown();
}
