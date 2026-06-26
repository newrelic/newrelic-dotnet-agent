// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Parses request-queue-time headers set by upstream load balancers or ingress controllers
/// (X-Request-Start, X-Queue-Start) and returns the time the request spent queued before
/// reaching the application. Unit auto-detection handles ns/us/ms/s epoch timestamps.
/// Future timestamps (clock skew) yield null, as do queue times beyond a sanity cap
/// (a misdetected unit or a stale/garbage timestamp produces an implausibly large delta).
/// Parsing avoids regex and per-call allocations because it runs on every web request.
/// </summary>
public static class QueueTimeHeaderParser
{
    private const string HeaderRequestStart = "X-Request-Start";
    private const string HeaderQueueStart = "X-Queue-Start";

    private static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Upper bound on a plausible front-end queue (load balancer + network + host accept).
    // A genuine wait is seconds, not minutes; a larger delta means the timestamp was
    // misdetected, stale, or garbage, so we omit rather than report a bogus queue time.
    private static readonly TimeSpan _maxQueueTime = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Reads X-Request-Start and X-Queue-Start headers via <paramref name="getHeader"/>,
    /// parses Unix-epoch timestamps (auto-detects ns/us/ms/s by magnitude), and returns
    /// the queue time as <c>nowUtc - earliestStartTime</c>. Returns null when no valid
    /// header is found, the start time is in the future relative to <paramref name="nowUtc"/>
    /// (clock skew guard), or the computed queue time exceeds the sanity cap.
    /// </summary>
    /// <param name="getHeader">Delegate that returns a header value by name, or null/empty if absent.</param>
    /// <param name="nowUtc">The current UTC time used to compute elapsed queue time.</param>
    public static TimeSpan? TryGetQueueTime(Func<string, string> getHeader, DateTime nowUtc)
    {
        var earliest = SelectEarlier(null, getHeader(HeaderRequestStart));
        earliest = SelectEarlier(earliest, getHeader(HeaderQueueStart));

        if (earliest == null || earliest.Value > nowUtc)
            return null;

        var queueTime = nowUtc - earliest.Value;
        if (queueTime > _maxQueueTime)
            return null;

        return queueTime;
    }

    private static DateTime? SelectEarlier(DateTime? current, string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return current;

        var candidate = ParseHeaderValue(raw);
        if (candidate == null)
            return current;

        if (current == null || candidate.Value < current.Value)
            return candidate;

        return current;
    }

    // Prefers the token after "t=" (allowing a leading server token and trailing data, e.g.
    // "host t=123, h=abc"); otherwise the entire trimmed value must be numeric.
    private static DateTime? ParseHeaderValue(string raw)
    {
        string number;
        var tIdx = raw.IndexOf("t=", StringComparison.Ordinal);
        if (tIdx >= 0)
            number = ExtractNumber(raw, tIdx + 2, requireRemainderEmpty: false);
        else
            number = ExtractNumber(raw, 0, requireRemainderEmpty: true);

        if (number == null)
            return null;

        if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return null;

        if (double.IsNaN(value) || double.IsInfinity(value))
            return null;

        // Auto-detect the timestamp unit by magnitude and normalize to seconds. A current
        // Unix epoch is ~1.7e9 in seconds (10 digits), ~1.7e12 in ms (13), ~1.7e15 in us (16),
        // and ~1.7e18 in ns (19). Each threshold sits an order of magnitude below its bucket
        // so the correct unit wins; values below 1e12 are taken as already being in seconds.
        double seconds;
        if (value >= 1e18)
            seconds = value / 1e9; // nanoseconds -> seconds
        else if (value >= 1e15)
            seconds = value / 1e6; // microseconds -> seconds
        else if (value >= 1e12)
            seconds = value / 1e3; // milliseconds -> seconds
        else
            seconds = value;       // already seconds

        DateTime startTime;
        try
        {
            startTime = _epoch.AddSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }

        return startTime;
    }

    // Scans an optional-fractional decimal number starting at startIndex (after skipping leading
    // whitespace). Returns the numeric substring, or null if no digits are present. When
    // requireRemainderEmpty is true, anything other than trailing whitespace after the number
    // makes the value invalid (mirrors a fully-anchored bare-number match).
    private static string ExtractNumber(string raw, int startIndex, bool requireRemainderEmpty)
    {
        var len = raw.Length;
        var i = startIndex;

        while (i < len && char.IsWhiteSpace(raw[i]))
            i++;

        var numStart = i;
        var seenDot = false;
        while (i < len)
        {
            var c = raw[i];
            if (c >= '0' && c <= '9')
            {
                i++;
            }
            else if (c == '.' && !seenDot)
            {
                seenDot = true;
                i++;
            }
            else
            {
                break;
            }
        }

        if (i == numStart)
            return null;

        var numEnd = i;

        // Reject a token that is only "." with no digits.
        if (numEnd - numStart == 1 && raw[numStart] == '.')
            return null;

        if (requireRemainderEmpty)
        {
            var j = numEnd;
            while (j < len && char.IsWhiteSpace(raw[j]))
                j++;
            if (j != len)
                return null;
        }

        return raw.Substring(numStart, numEnd - numStart);
    }
}
