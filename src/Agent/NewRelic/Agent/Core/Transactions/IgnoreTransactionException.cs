// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Transactions;

/// <summary>
/// Thrown by the transaction name normalizer to indicate that a transaction should be ignored.
/// </summary>
public class IgnoreTransactionException : Exception
{
    /// <summary>
    /// The name that was ignored
    /// </summary>
    public readonly string IgnoredTransactionName;

    public IgnoreTransactionException(string message, string ignoredTransactionName) : base(message)
    {
        IgnoredTransactionName = ignoredTransactionName;
    }
}
