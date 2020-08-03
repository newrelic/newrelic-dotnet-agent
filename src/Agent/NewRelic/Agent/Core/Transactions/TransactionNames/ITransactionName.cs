// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public interface ITransactionName
    {
        bool IsWeb { get; }
    }
}
