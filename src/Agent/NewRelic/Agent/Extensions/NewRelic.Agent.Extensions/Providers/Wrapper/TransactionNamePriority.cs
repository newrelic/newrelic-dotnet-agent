// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public enum TransactionNamePriority
    {
        Uri = 1,
        StatusCode = 2,
        Handler = 3,
        Route = 4,
        FrameworkLow = 5,
        FrameworkHigh = 6,
        CustomTransactionName = 8,
        UserTransactionName = int.MaxValue
    }
}
