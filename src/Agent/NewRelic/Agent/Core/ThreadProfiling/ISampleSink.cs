// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface ISampleSink
    {
        void SampleAcquired(ThreadSnapshot[] threadSnapshots);
        void SamplingComplete();
    }
}
