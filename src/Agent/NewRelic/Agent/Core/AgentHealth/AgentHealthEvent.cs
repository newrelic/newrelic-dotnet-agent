// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.AgentHealth
{
    public enum AgentHealthEvent
    {
        TransactionGarbageCollected,
        WrapperShutdown,
        Transaction,
        Log,
        Custom,
        Error,
        Span,
        InfiniteTracingSpan
    }
}
