// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Time
{
    [TestFixture]
    public class DateTimeMathTests
    {
        [Test]
        [TestCase(1, 1, 1)]
        [TestCase(2, 3, 2)]
        [TestCase(3, 2, 2)]
        public void Min_ReturnsMinTime(int seconds1, int seconds2, int expectedSeconds)
        {
            var baseTime = DateTime.Now;
            var time1 = baseTime.AddSeconds(seconds1);
            var time2 = baseTime.AddSeconds(seconds2);
            var expectedTime = baseTime.AddSeconds(expectedSeconds);

            var actualTime = DateTimeMath.Min(time1, time2);

            Assert.That(actualTime, Is.EqualTo(expectedTime));
        }

        [Test]
        [TestCase(1, 1, 1)]
        [TestCase(2, 3, 3)]
        [TestCase(3, 2, 3)]
        public void Max_ReturnsMaxTime(int seconds1, int seconds2, int expectedSeconds)
        {
            var baseTime = DateTime.Now;
            var time1 = baseTime.AddSeconds(seconds1);
            var time2 = baseTime.AddSeconds(seconds2);
            var expectedTime = baseTime.AddSeconds(expectedSeconds);

            var actualTime = DateTimeMath.Max(time1, time2);

            Assert.That(actualTime, Is.EqualTo(expectedTime));
        }
    }
}
