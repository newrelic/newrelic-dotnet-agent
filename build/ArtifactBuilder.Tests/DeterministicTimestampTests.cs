// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace ArtifactBuilder.Tests;

[TestFixture]
public class DeterministicTimestampTests
{
    [Test]
    public void Resolve_PrefersSourceDateEpochOverCommitTime()
    {
        var result = DeterministicTimestamp.Resolve(_ => "1700000000", () => "1600000000\n");

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000)));
    }

    [Test]
    public void Resolve_UsesCommitTimeWhenSourceDateEpochIsMissing()
    {
        var result = DeterministicTimestamp.Resolve(_ => null, () => "1600000000\n");

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1600000000)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-a-number")]
    [TestCase("-5")]
    [TestCase("99999999999999")]
    public void Resolve_FallsBackToFixedConstantWhenNothingUsable(string value)
    {
        var result = DeterministicTimestamp.Resolve(_ => value, () => value);

        Assert.That(result, Is.EqualTo(DeterministicTimestamp.FallbackTimestamp));
    }

    [Test]
    public void Resolve_IgnoresUnusableSourceDateEpochAndUsesCommitTime()
    {
        var result = DeterministicTimestamp.Resolve(_ => "garbage", () => "1600000000");

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1600000000)));
    }

    [Test]
    public void ToZipTimestamp_RoundsDownToEvenSecondInUtc()
    {
        var result = DeterministicTimestamp.ToZipTimestamp(new DateTimeOffset(2024, 5, 6, 12, 30, 41, TimeSpan.FromHours(-5)));

        Assert.That(result, Is.EqualTo(new DateTimeOffset(2024, 5, 6, 17, 30, 40, TimeSpan.Zero)));
        Assert.That(result.Offset, Is.EqualTo(TimeSpan.Zero));
    }

    [Test]
    public void ToZipTimestamp_ClampsToSupportedRange()
    {
        Assert.That(DeterministicTimestamp.ToZipTimestamp(DateTimeOffset.FromUnixTimeSeconds(0)).Year, Is.EqualTo(1980));
        Assert.That(DeterministicTimestamp.ToZipTimestamp(new DateTimeOffset(3000, 1, 1, 0, 0, 0, TimeSpan.Zero)).Year, Is.EqualTo(2107));
    }
}
