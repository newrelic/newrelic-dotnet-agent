// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling;

public interface IThreadProfilingSampler
{
    bool Start(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods);
    void Stop();
}