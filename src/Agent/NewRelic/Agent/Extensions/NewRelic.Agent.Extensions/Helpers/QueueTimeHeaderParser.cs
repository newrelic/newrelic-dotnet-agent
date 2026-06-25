// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Globalization;

namespace NewRelic.Agent.Extensions.Helpers;

/// <summary>
/// Parses request-queue-time headers set by upstream load balancers or ingress controllers
/// (X-Request-Start, X-Queue-Start) and returns the time the request spent queued before
/// reaching the application. Unit auto-detection handles ns/us/ms/s epoch timestamps.
/// Values earlier than 2000-01-01 are rejected. Future timestamps (clock skew) yield null.
/// Parsing avoids regex and per-call allocations because it runs on every web request.
/// </summary>
public static class QueueTimeHeaderParser
{
    private const string HeaderRequestStart = "X-Request-Start";
    private const string HeaderQueueStart = "X-Queue-Start";

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
        var earliest = SelectEarlier(null, getHeader(HeaderRequestStart));
        earliest = SelectEarlier(earliest, getHeader(HeaderQueueStart));

        if (earliest == null || earliest.Value > nowUtc)
            return null;

        return nowUtc - earliest.Value;
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
