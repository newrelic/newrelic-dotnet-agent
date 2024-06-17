// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.DistributedTracing
{
    public enum DistributedTracingParentType
    {
        Unknown = -1,
        App = 0,
        Browser = 1,
        Mobile = 2
    }
}
