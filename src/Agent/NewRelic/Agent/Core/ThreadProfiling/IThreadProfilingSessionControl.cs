// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

public interface IThreadProfilingSessionControl
{
    bool StartThreadProfilingSession(int profileSessionId, uint frequencyInMsec, uint durationInMsec);
    bool StopThreadProfilingSession(int profileId, bool reportData = true);
    bool IgnoreMinMinimumSamplingDuration { get; }
}