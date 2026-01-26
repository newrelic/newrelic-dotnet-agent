// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

internal class NoOpDistributedTracePayload : IDistributedTracePayload
{
    public string HttpSafe()
    {
        return string.Empty;
    }

    public string Text()
    {
        return string.Empty;
    }

    public bool IsEmpty()
    {
        return true;
    }
}