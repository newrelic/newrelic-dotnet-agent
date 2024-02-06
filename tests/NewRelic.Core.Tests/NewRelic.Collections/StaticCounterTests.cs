// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Collections;
using NUnit.Framework;

namespace NewRelic.Core.Tests.NewRelic.Collections
{
    class StaticCounterTests
    {

        [Test]
        public void StaticCounterTests_Battery()
        {
            const long nextIters = 1000L;
            StaticCounter.Reset();
            Assert.That(StaticCounter.Value, Is.EqualTo(0), "counter should be 0 after Reset()");
            for (var counter = 1L; counter < nextIters; ++counter)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(StaticCounter.Next(), Is.EqualTo(counter), "Next() should return the next value in the sequence");
                    Assert.That(StaticCounter.Value, Is.EqualTo(counter), "Value should return current value");
                });
            }

            Assert.Multiple(() =>
            {
                Assert.That(StaticCounter.Next(), Is.EqualTo(nextIters), "Next() should return the next value in the sequence");
                Assert.That(StaticCounter.Reset(), Is.EqualTo(nextIters), "Reset() should return the value before being set to 0");
                Assert.That(StaticCounter.Value, Is.EqualTo(0), "The result of Reset() should be a value of 0");
            });
        }
    }
}
