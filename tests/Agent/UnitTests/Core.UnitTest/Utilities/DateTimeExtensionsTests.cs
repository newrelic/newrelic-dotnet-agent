// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using NUnit.Framework;
using System;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class DateTimeExtensionsTests
    {
        [Test]
        public void DoubleToDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = (-23890247268d).ToDateTime();

            Assert.That(dateTime, Is.EqualTo(new DateTime(1212, 12, 12, 12, 12, 12)));
        }

        [Test]
        public void DateTimeToUnixTime_ReturnsCorrectUnixTime()
        {
            var unixTime = new DateTime(1212, 12, 12, 12, 12, 12).ToUnixTimeSeconds();

            Assert.That(unixTime, Is.EqualTo(-23890247268d));
        }


        [Test]
        public void DateTimeToUnixTimeMillisecond_Epoch()
        {
            var unixTimeMilliseconds = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToUnixTimeMilliseconds();

            Assert.That(unixTimeMilliseconds, Is.EqualTo(0));
        }

        [Test]
        public void DateTimeToUnixTimeMillisecond_EpochPlusOneSecond()
        {
            var unixTimeMilliseconds = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc).ToUnixTimeMilliseconds();

            Assert.That(unixTimeMilliseconds, Is.EqualTo(1000));
        }

        [Test]
        public void DateTimeToUnixTimeMillisecond_20180704_120000_Local()
        {
            var expected = new DateTime(2018, 7, 4, 12, 0, 0, DateTimeKind.Local);
            var unixTimeMilliseconds = expected.ToUnixTimeMilliseconds();
            var actual = unixTimeMilliseconds.FromUnixTimeMilliseconds().ToLocalTime();
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void DateTimeFromUnixTimeMillisecond_Epoch()
        {
            var expected = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var actual = 0L.FromUnixTimeMilliseconds();
            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
