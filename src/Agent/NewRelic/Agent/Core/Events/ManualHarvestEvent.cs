// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Events;

public class ManualHarvestEvent
{
    public string TransactionId;
    public ManualHarvestEvent(string transactionId)
    {
        TransactionId = transactionId;
    }
}