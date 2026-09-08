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
    void Stop();
}