// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests
{
    public class TimeSpanExtensionsTests
    {
        [Test]
        public void multiply_integer()
        {
            var expected = TimeSpan.FromSeconds(2);
            var oneSecond = TimeSpan.FromSeconds(1);

            var sactual = oneSecond.Multiply(2);

            Assert.That(sactual, Is.EqualTo(expected));
        }

        [Test]
        public void multiply_float()
        {
            var expected = TimeSpan.FromSeconds(3);
            var twoSeconds = TimeSpan.FromSeconds(2);

            var actual = twoSeconds.Multiply(1.5);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void nullable_double_to_time_span()
        {
            var expected = TimeSpan.FromSeconds(3.5);
            double? inputFloat = 3.5;

            var actual = TimeSpanExtensions.FromSeconds(inputFloat);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void null_double_to_time_span()
        {
            double? inputFloat = null;

            var actual = TimeSpanExtensions.FromSeconds(inputFloat);

            Assert.That(actual, Is.EqualTo(null));
        }
    }
}
