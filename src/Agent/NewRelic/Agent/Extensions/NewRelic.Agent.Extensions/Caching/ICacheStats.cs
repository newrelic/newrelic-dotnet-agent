// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Caching
{
    public interface ICacheStats
    {
        int Size { get; }
        int Capacity { get; }
        int CountHits { get; }
        int CountMisses { get; }
        int CountEjections { get; }
        void ResetStats();
    }
}
