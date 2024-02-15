// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Events
{
    public class FlushServerlessDataEvent
    {
        public string TransactionId;
        public FlushServerlessDataEvent(string transactionId)
        {
            TransactionId = transactionId;
        }
    }
}
