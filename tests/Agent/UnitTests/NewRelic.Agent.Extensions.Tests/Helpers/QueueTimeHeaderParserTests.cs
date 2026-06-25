// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class QueueTimeHeaderParserTests
{
    // Fixed reference time: 2026-01-01 00:00:00 UTC
    private static readonly DateTime NowUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static Func<string, string> MakeGetter(Dictionary<string, string> headers)
    {
        return name => headers.TryGetValue(name, out var v) ? v : null;
    }

    private static long ToMs(DateTime utc) => (long)(utc - Epoch).TotalMilliseconds;
    private static long ToSec(DateTime utc) => (long)(utc - Epoch).TotalSeconds;
    private static long ToUs(DateTime utc) => (long)(utc - Epoch).TotalMilliseconds * 1000;
    private static long ToNs(DateTime utc) => (long)(utc - Epoch).TotalMilliseconds * 1_000_000;

    [Test]
    public void TPrefix_Milliseconds_ReturnsCorrectQueueTime()
    {
        var startTime = NowUtc.AddSeconds(-5);
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(startTime)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.TotalMilliseconds, Is.EqualTo(5000.0).Within(1.0));
    }

    [Test]
    public void BareNumber_Seconds_WithFractional_ReturnsCorrectQueueTime()
    {
        var startTime = NowUtc.AddSeconds(-10);
        var secValue = (startTime - Epoch).TotalSeconds;
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"{secValue:F3}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.TotalMilliseconds, Is.EqualTo(10000.0).Within(1.0));
    }

    [Test]
    public void ServerTokenPlusTPrefix_ParsedCorrectly()
    {
        var startTime = NowUtc.AddSeconds(-3);
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"web01 t={ToMs(startTime)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.TotalMilliseconds, Is.EqualTo(3000.0).Within(1.0));
    }

    [Test]
    public void AllUnitBuckets_SameInstant_YieldApproximatelySameQueueTime()
    {
        var startTime = NowUtc.AddSeconds(-7);
        double expectedMs = 7000.0;

        var secHeaders = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"{ToSec(startTime)}"
        };
        var msHeaders = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(startTime)}"
        };
        var usHeaders = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToUs(startTime)}"
        };
        var nsHeaders = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToNs(startTime)}"
        };

        var secResult = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(secHeaders), NowUtc);
        var msResult = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(msHeaders), NowUtc);
        var usResult = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(usHeaders), NowUtc);
        var nsResult = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(nsHeaders), NowUtc);

        Assert.That(secResult, Is.Not.Null);
        Assert.That(msResult, Is.Not.Null);
        Assert.That(usResult, Is.Not.Null);
        Assert.That(nsResult, Is.Not.Null);

        Assert.That(secResult.Value.TotalMilliseconds, Is.EqualTo(expectedMs).Within(1000.0)); // seconds bucket loses sub-second
        Assert.That(msResult.Value.TotalMilliseconds, Is.EqualTo(expectedMs).Within(1.0));
        Assert.That(usResult.Value.TotalMilliseconds, Is.EqualTo(expectedMs).Within(1.0));
        Assert.That(nsResult.Value.TotalMilliseconds, Is.EqualTo(expectedMs).Within(1.0));
    }

    [Test]
    public void EarliestWins_BothHeadersPresent()
    {
        var earlier = NowUtc.AddSeconds(-20);
        var later = NowUtc.AddSeconds(-5);
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(later)}",
            ["X-Queue-Start"] = $"t={ToMs(earlier)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.TotalMilliseconds, Is.EqualTo(20000.0).Within(1.0));
    }

    [Test]
    public void OnlyQueueStart_Present_StillWorks()
    {
        var startTime = NowUtc.AddSeconds(-8);
        var headers = new Dictionary<string, string>
        {
            ["X-Queue-Start"] = $"t={ToMs(startTime)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value.TotalMilliseconds, Is.EqualTo(8000.0).Within(1.0));
    }

    [Test]
    public void Pre2000Timestamp_ReturnsNull()
    {
        // 1999-12-31 in milliseconds
        var pre2000 = new DateTime(1999, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(pre2000)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void FutureTimestamp_ReturnsNull()
    {
        var future = NowUtc.AddSeconds(60);
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(future)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void MissingHeaders_ReturnsNull()
    {
        var headers = new Dictionary<string, string>();

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void EmptyHeaderValue_ReturnsNull()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = "   "
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void GarbageValue_ReturnsNull()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = "not-a-timestamp-at-all"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void NullHeaderDelegate_ReturnsNull()
    {
        var result = QueueTimeHeaderParser.TryGetQueueTime(_ => null, NowUtc);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void EqualStartAndNow_ReturnsZero()
    {
        var headers = new Dictionary<string, string>
        {
            ["X-Request-Start"] = $"t={ToMs(NowUtc)}"
        };

        var result = QueueTimeHeaderParser.TryGetQueueTime(MakeGetter(headers), NowUtc);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Value, Is.EqualTo(TimeSpan.Zero));
    }
}
