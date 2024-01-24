// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Time
{
    [TestFixture]
    public class TimeSpanMathTests
    {
        [Test]
        [TestCase(1, 1, 1)]
        [TestCase(2, 3, 2)]
        [TestCase(3, 2, 2)]
        public void Min_ReturnsMinTime(int seconds1, int seconds2, int expectedSeconds)
        {
            var time1 = TimeSpan.FromSeconds(seconds1);
            var time2 = TimeSpan.FromSeconds(seconds2);
            var expectedTime = TimeSpan.FromSeconds(expectedSeconds);

            var actualTime = TimeSpanMath.Min(time1, time2);

            Assert.That(actualTime, Is.EqualTo(expectedTime));
        }

        [Test]
        [TestCase(1, 1, 1)]
        [TestCase(2, 3, 3)]
        [TestCase(3, 2, 3)]
        public void Max_ReturnsMaxTime(int seconds1, int seconds2, int expectedSeconds)
        {
            var time1 = TimeSpan.FromSeconds(seconds1);
            var time2 = TimeSpan.FromSeconds(seconds2);
            var expectedTime = TimeSpan.FromSeconds(expectedSeconds);

            var actualTime = TimeSpanMath.Max(time1, time2);

            Assert.That(actualTime, Is.EqualTo(expectedTime));
        }
    }
}
