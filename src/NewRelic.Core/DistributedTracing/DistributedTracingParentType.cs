/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Core.DistributedTracing
{
    public enum DistributedTracingParentType
    {
        Unknown = -1,
        App = 0,
        Browser = 1,
        Mobile = 2
    }
}
