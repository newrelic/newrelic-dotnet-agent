// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Extension methods for applying queue-time header parsing to an <see cref="ITransaction"/>.
/// </summary>
public static class TransactionQueueTimeExtensions
{
    /// <summary>
    /// Reads X-Request-Start / X-Queue-Start headers via <paramref name="getHeader"/>, parses the
    /// queue time, and calls <see cref="ITransaction.SetQueueTime"/> when a valid value is found.
    /// Returns <c>true</c> when queue time was set; <c>false</c> when no valid header was present
    /// or an exception occurred (logged at finest level).
    /// </summary>
    /// <param name="transaction">The current transaction.</param>
    /// <param name="getHeader">Delegate that returns a header value by name, or null/empty if absent.</param>
    public static bool TrySetQueueTimeFromHeaders(this ITransaction transaction, Func<string, string> getHeader)
    {
        try
        {
            var queueTime = QueueTimeHeaderParser.TryGetQueueTime(getHeader, DateTime.UtcNow);
            if (queueTime.HasValue)
            {
                transaction.SetQueueTime(queueTime.Value);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Log.Finest(ex, "Failed to set queue time from request headers.");
            return false;
        }
    }
}
