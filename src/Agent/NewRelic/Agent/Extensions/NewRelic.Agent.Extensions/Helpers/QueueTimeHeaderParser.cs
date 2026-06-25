// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Parses request-queue-time headers set by upstream load balancers or ingress controllers
/// (X-Request-Start, X-Queue-Start) and returns the time the request spent queued before
/// reaching the application. Unit auto-detection handles ns/us/ms/s epoch timestamps.
/// Values earlier than 2000-01-01 are rejected. Future timestamps (clock skew) yield null.
/// </summary>
public static class QueueTimeHeaderParser
{
    private const string HeaderRequestStart = "X-Request-Start";
    private const string HeaderQueueStart = "X-Queue-Start";

    private static readonly Regex _tEqualsPattern =
        new Regex(@"t=(\d+(?:\.\d+)?)", RegexOptions.Compiled);

    private static readonly Regex _bareNumberPattern =
        new Regex(@"^\s*(\d+(?:\.\d+)?)\s*$", RegexOptions.Compiled);

    private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _floor = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Reads X-Request-Start and X-Queue-Start headers via <paramref name="getHeader"/>,
    /// parses Unix-epoch timestamps (auto-detects ns/us/ms/s by magnitude), and returns
    /// the queue time as <c>nowUtc - earliestStartTime</c>. Returns null when no valid
    /// header is found, all candidates predate 2000-01-01, or the start time is in the
    /// future relative to <paramref name="nowUtc"/> (clock skew guard).
    /// </summary>
    /// <param name="getHeader">Delegate that returns a header value by name, or null/empty if absent.</param>
    /// <param name="nowUtc">The current UTC time used to compute elapsed queue time.</param>
    public static TimeSpan? TryGetQueueTime(Func<string, string> getHeader, DateTime nowUtc)
    {
        DateTime? earliest = null;

        foreach (var headerName in new[] { HeaderRequestStart, HeaderQueueStart })
        {
            var raw = getHeader(headerName);
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var candidate = ParseHeaderValue(raw);
            if (candidate == null)
                continue;

            if (earliest == null || candidate.Value < earliest.Value)
                earliest = candidate;
        }

        if (earliest == null)
            return null;

        if (earliest.Value > nowUtc)
            return null;

        return nowUtc - earliest.Value;
    }

    private static DateTime? ParseHeaderValue(string raw)
    {
        string captured = null;

        var tMatch = _tEqualsPattern.Match(raw);
        if (tMatch.Success)
        {
            captured = tMatch.Groups[1].Value;
        }
        else
        {
            var bareMatch = _bareNumberPattern.Match(raw);
            if (bareMatch.Success)
                captured = bareMatch.Groups[1].Value;
        }

        if (captured == null)
            return null;

        if (!double.TryParse(captured, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        if (double.IsNaN(value) || double.IsInfinity(value))
            return null;

        double seconds;
        if (value >= 1e18)
            seconds = value / 1e9;
        else if (value >= 1e15)
            seconds = value / 1e6;
        else if (value >= 1e12)
            seconds = value / 1e3;
        else
            seconds = value;

        DateTime startTime;
        try
        {
            startTime = _epoch.AddSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        if (startTime < _floor)
            return null;

        return startTime;
    }
}
