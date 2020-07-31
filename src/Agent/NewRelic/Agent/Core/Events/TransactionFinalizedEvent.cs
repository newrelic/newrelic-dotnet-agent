// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;

namespace NewRelic.Agent.Core.Events
{
    public class TransactionFinalizedEvent
    {
        public readonly ITransaction Transaction;

        public TransactionFinalizedEvent(ITransaction transaction)
        {
            Transaction = transaction;
        }
    }
}
