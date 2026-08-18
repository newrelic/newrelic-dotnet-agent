// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Extensions.Providers.Wrapper;

/// <summary>
/// Detects whether an outbound carrier already carries New Relic distributed-trace or CAT headers -
/// that is, whether another instrumentation has already traced and instrumented the request. The
/// HttpWebRequest wrappers use this as a reliable, request-scoped ownership signal: unlike the
/// transaction's current segment, the header set travels with the request across async and thread
/// boundaries, so it stays accurate where CurrentSegment.IsExternal does not.
/// </summary>
public static class TracingHeaderExistence
{
    // Outbound CAT request header names. CAT's wire names are fixed by the protocol; Core defines the
    // same strings privately in CatHeaderHandler, so they are mirrored here for the wrapper side.
    private const string CatIdHeader = "X-NewRelic-ID";
    private const string CatTransactionHeader = "X-NewRelic-Transaction";

    /// <summary>
    /// Returns true if any of the supplied header keys is a New Relic distributed-trace
    /// (traceparent / tracestate / newrelic) or CAT (X-NewRelic-ID / X-NewRelic-Transaction) header.
    /// Comparison is case-insensitive. Null or empty input returns false.
    /// </summary>
    public static bool ContainsTracingHeader(IEnumerable<string> headerKeys)
    {
        if (headerKeys == null)
        {
            return false;
        }

        foreach (var key in headerKeys)
        {
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // OrdinalIgnoreCase on the lowercase "newrelic" also matches the NEWRELIC / Newrelic variants.
            if (string.Equals(key, Constants.TraceParentHeaderKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Constants.TraceStateHeaderKey, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, Constants.DistributedTracePayloadKeyAllLower, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, CatIdHeader, StringComparison.OrdinalIgnoreCase)
                || string.Equals(key, CatTransactionHeader, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
