// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Transactions;

namespace NewRelic.Agent.Core.Events;

public class TransactionFinalizedEvent
{
    public readonly IInternalTransaction Transaction;

    public TransactionFinalizedEvent(IInternalTransaction transaction)
    {
        Transaction = transaction;
    }
}