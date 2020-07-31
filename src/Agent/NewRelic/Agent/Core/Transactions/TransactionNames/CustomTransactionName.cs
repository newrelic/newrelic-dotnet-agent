// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class CustomTransactionName : ITransactionName
    {
        public readonly string Name;

        public bool IsWeb { get; }

        public CustomTransactionName(string name, bool isWeb)
        {
            Name = name;
            IsWeb = isWeb;
        }

    }
}
