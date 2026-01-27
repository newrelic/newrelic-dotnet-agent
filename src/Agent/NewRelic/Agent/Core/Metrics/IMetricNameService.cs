// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;

namespace NewRelic.Agent.Core.Metrics;

public interface IMetricNameService : IDisposable
{
    /// <summary>
    /// Returns the result of running the given metric name through a set of renaming rules.  If the metric name matched
    /// an "ignore" rule, null is returned.
    /// </summary>
    /// <param name="metricName"></param>
    /// <returns>The original metric name if no rules matched the metric name, a new name if rules were applied, or null
    /// if the metric name matched an "ignore" rule.</returns>
    string RenameMetric(string metricName);

    /// <summary>
    /// Normalizes a url using the url rules sent from the New Relic service.  All query parameters will be stripped
    /// from the url.
    /// </summary>
    /// <param name="url"></param>
    /// <returns></returns>
    string NormalizeUrl(string url);

    /// <summary>
    /// Attempts to rename a transaction by passing the transaction metric name through a set of renaming rules. The metric name will be marked as "ShouldIgnore" if the name matched an ignore rule.
    /// </summary>
    /// <param name="proposedTransactionName">The transaction name that is to be renamed.</param>
    /// <returns>A new transaction name.</returns>
    TransactionMetricName RenameTransaction(TransactionMetricName proposedTransactionName);

    /// <summary>
    /// Returns an Apdex_t value associated with a Named Transaction if one is found, null if not.
    /// </summary>
    /// <param name="transactionName">The transaction name for the Named Transaction.</param>
    /// <returns>A <type name="TimeSpan?"/> representing the apdex_t value. If no Named Transation exists which matches 
    /// <paramref name="transactionName"/> then null is returned."/></returns>
    TimeSpan? TryGetApdex_t(string transactionName);

}