/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
namespace NewRelic.Agent.Core.Transactions.TransactionNames
{
    public class WebTransactionName : ITransactionName
    {
        public readonly string Category;
        public readonly string Name;

        public WebTransactionName(string category, string name)
        {
            Category = category;
            Name = name;
        }

        public bool IsWeb { get { return true; } }
    }
}
