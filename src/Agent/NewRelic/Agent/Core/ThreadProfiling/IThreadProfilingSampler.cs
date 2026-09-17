// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

public interface IThreadProfilingSampler
{
    /// <summary>
    /// True while the background sampling worker thread is actually running. Set atomically when the
    /// worker is created and cleared in the worker's own <c>finally</c> when it exits, so it tracks the
    /// worker's real lifetime rather than a request- or completion-time bookkeeping value.
    /// </summary>
    bool IsRunning { get; }

    bool Start(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods);

    /// <summary>
    /// Signals the background sampling worker to terminate and waits (bounded) for it to wind down.
    /// Returns <c>true</c> only when the worker is confirmed stopped -- i.e. it is safe for the caller to
    /// mutate the shared state the worker reads during its end-of-session aggregation. Returns
    /// <c>false</c> when the bounded join timed out and the worker may still be running, so the caller
    /// must not touch that shared state.
    /// </summary>
    bool Stop();
}