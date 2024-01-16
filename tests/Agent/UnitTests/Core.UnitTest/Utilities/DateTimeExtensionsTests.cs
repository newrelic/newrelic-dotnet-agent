// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;

namespace NewRelic.Agent.Core.Utilities
{
    [TestFixture]
    public class DateTimeExtensionsTests
    {
        [Test]
        public void DoubleToDateTime_ReturnsCorrectDateTime()
        {
            var dateTime = (-23890247268d).ToDateTime();

            ClassicAssert.AreEqual(new DateTime(1212, 12, 12, 12, 12, 12), dateTime);
        }

        [Test]
        public void DateTimeToUnixTime_ReturnsCorrectUnixTime()
        {
            var unixTime = new DateTime(1212, 12, 12, 12, 12, 12).ToUnixTimeSeconds();

            ClassicAssert.AreEqual(-23890247268d, unixTime);
        }


        [Test]
        public void DateTimeToUnixTimeMillisecond_Epoch()
        {
            var unixTimeMilliseconds = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToUnixTimeMilliseconds();

            ClassicAssert.AreEqual(0, unixTimeMilliseconds);
        }

        [Test]
        public void DateTimeToUnixTimeMillisecond_EpochPlusOneSecond()
        {
            var unixTimeMilliseconds = new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Utc).ToUnixTimeMilliseconds();

            ClassicAssert.AreEqual(1000, unixTimeMilliseconds);
        }

        [Test]
        public void DateTimeToUnixTimeMillisecond_20180704_120000_Local()
        {
            var expected = new DateTime(2018, 7, 4, 12, 0, 0, DateTimeKind.Local);
            var unixTimeMilliseconds = expected.ToUnixTimeMilliseconds();
            var actual = unixTimeMilliseconds.FromUnixTimeMilliseconds().ToLocalTime();
            ClassicAssert.AreEqual(expected, actual);
        }

        [Test]
        public void DateTimeFromUnixTimeMillisecond_Epoch()
        {
            var expected = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var actual = 0L.FromUnixTimeMilliseconds();
            ClassicAssert.AreEqual(expected, actual);
        }
    }
}
