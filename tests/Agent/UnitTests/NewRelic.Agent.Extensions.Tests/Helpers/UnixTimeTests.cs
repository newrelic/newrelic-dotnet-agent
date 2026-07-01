// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers;

[TestFixture]
public class UnixTimeTests
{
    [Test]
    public void FromSeconds_Zero_ReturnsEpoch()
    {
        var expected = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(UnixTime.FromSeconds(0d), Is.EqualTo(expected));
    }

    [Test]
    public void ToSeconds_Epoch_ReturnsZero()
    {
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.That(UnixTime.ToSeconds(epoch), Is.EqualTo(0d));
    }

    [Test]
    public void FromSeconds_NegativeValue_ReturnsCorrectDate()
    {
        // Mirrors DateTimeExtensionsTests: -23890247268 seconds from epoch = 1212-12-12 12:12:12
        var expected = new DateTime(1212, 12, 12, 12, 12, 12);
        Assert.That(UnixTime.FromSeconds(-23890247268d), Is.EqualTo(expected));
    }

    [Test]
    public void ToSeconds_EarlyDate_ReturnsCorrectNegativeValue()
    {
        // Mirrors DateTimeExtensionsTests: 1212-12-12 12:12:12 = -23890247268 seconds from epoch
        var date = new DateTime(1212, 12, 12, 12, 12, 12);
        Assert.That(UnixTime.ToSeconds(date), Is.EqualTo(-23890247268d));
    }
}
