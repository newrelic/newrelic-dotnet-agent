// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Tests.TestSerializationHelpers.Models
{
    public class TransactionTraceComparisonResult
    {
        public TransactionTraceComparisonResult(bool isEquivalent, string diff)
        {
            IsEquivalent = isEquivalent;
            Diff = diff;
        }
        public bool IsEquivalent { get; }
        public string Diff { get; }
    }
}
