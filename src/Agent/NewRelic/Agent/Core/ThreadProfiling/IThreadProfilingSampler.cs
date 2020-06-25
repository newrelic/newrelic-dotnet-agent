/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.ThreadProfiling
{
    public interface IThreadProfilingSampler
    {
        bool Start(uint frequencyInMsec, uint durationInMsec, ISampleSink sampleSink, INativeMethods nativeMethods);
        void Stop();
    }
}
