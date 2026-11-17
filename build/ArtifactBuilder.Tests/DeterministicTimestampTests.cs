// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace ArtifactBuilder.Tests;

[TestFixture]
public class DeterministicTimestampTests
{
    [Test]
    public void Resolve_UsesValidSourceDateEpochAndAnnouncesIt()
    {
        var log = new List<string>();

        var result = DeterministicTimestamp.Resolve(_ => "1700000000", () => throw new InvalidOperationException("git must not be consulted"), log.Add);

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000)));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0], Does.Contain("2023-11-14T22:13:20Z").And.Contain("SOURCE_DATE_EPOCH"));
    }

    [Test]
    public void Resolve_PrefersSourceDateEpochOverCommitTime()
    {
        var result = DeterministicTimestamp.Resolve(_ => "1700000000", () => "1600000000" + Environment.NewLine, _ => { });

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1700000000)));
    }

    [TestCase("not-a-number")]
    [TestCase("-5")]
    [TestCase("1.5")]
    [TestCase("99999999999999")]
    public void Resolve_ThrowsWhenSourceDateEpochIsMalformed(string value)
    {
        var log = new List<string>();

        var exception = Assert.Throws<FormatException>(() => DeterministicTimestamp.Resolve(_ => value, () => "1600000000", log.Add));

        Assert.That(exception.Message, Does.Contain("SOURCE_DATE_EPOCH").And.Contain(value));
        Assert.That(log, Is.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("  ")]
    public void Resolve_TreatsUnsetOrEmptySourceDateEpochAsNotSet(string value)
    {
        var result = DeterministicTimestamp.Resolve(_ => value, () => "1600000000" + Environment.NewLine, _ => { });

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1600000000)));
    }

    [Test]
    public void Resolve_UsesCommitTimeWhenSourceDateEpochIsMissingAndAnnouncesIt()
    {
        var log = new List<string>();

        var result = DeterministicTimestamp.Resolve(_ => null, () => "1600000000" + Environment.NewLine, log.Add);

        Assert.That(result, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1600000000)));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0], Does.Contain("2020-09-13T12:26:40Z").And.Contain("HEAD commit").And.Not.Contain("WARNING"));
    }

    [Test]
    public void Resolve_FallsBackWithWarningWhenCommitTimeCannotBeRead()
    {
        var log = new List<string>();

        var result = DeterministicTimestamp.Resolve(_ => null, () => throw new InvalidOperationException("dubious ownership"), log.Add);

        Assert.That(result, Is.EqualTo(DeterministicTimestamp.FallbackTimestamp));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0], Does.StartWith("WARNING").And.Contain("dubious ownership").And.Contain("2020-01-01T00:00:00Z"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("garbage")]
    public void Resolve_FallsBackWithWarningWhenCommitTimeIsNotANumber(string commitTime)
    {
        var log = new List<string>();

        var result = DeterministicTimestamp.Resolve(_ => null, () => commitTime, log.Add);

        Assert.That(result, Is.EqualTo(DeterministicTimestamp.FallbackTimestamp));
        Assert.That(log, Has.Count.EqualTo(1));
        Assert.That(log[0], Does.StartWith("WARNING"));
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
