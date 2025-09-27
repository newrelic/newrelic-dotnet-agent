// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Attributes
{
    public enum TypeAttributeValue
    {
        Transaction = 1,
        TransactionError = 2,
        Span = 3,
        SpanLink = 4,
        SpanEventEvent = 5
    }
}
