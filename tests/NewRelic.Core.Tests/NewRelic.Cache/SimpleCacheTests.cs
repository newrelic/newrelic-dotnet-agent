// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Core.Caching;
using NUnit.Framework;

namespace NewRelic.Core.Tests.NewRelic.Cache
{
    public class SimpleCacheTests
    {
        [Test]
        public void CacheReturnsCorrectValuesForKeys()
        {
            int capacity = 5;
            var cache = new SimpleCache<string, object>(capacity);

            var val1 = "value1";
            var val2 = "value2";
            var val3 = "value3";

            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key3", () => val3);

            Assert.Multiple(() =>
            {
                //Use AreSame to ensure that we are getting a reference match.
                Assert.That(cache.Peek("key1"), Is.SameAs(val1));
                Assert.That(cache.Peek("key2"), Is.SameAs(val2));
                Assert.That(cache.Peek("key3"), Is.SameAs(val3));
            });

            var expectedHits = 0;
            var expectedMisses = 3;
            var expectedEjections = 0;
            var expectedSize = 3;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void ItemsAreBeingCached()
        {
            var val1 = "value1";
            var val2 = "value2";
            var val3 = "value3";

            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);
            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key3", () => val3);

            //This should not modify the value of key2 
            var shouldbeVal2 = cache.GetOrAdd("key2", () => "xyz");

            Assert.Multiple(() =>
            {
                //Use AreSame to ensure that we are getting a reference match.
                Assert.That(cache.Peek("key1"), Is.SameAs(val1));
                Assert.That(cache.Peek("key2"), Is.SameAs(val2));
                Assert.That(cache.Peek("key3"), Is.SameAs(val3));

                Assert.That(val2, Is.SameAs(shouldbeVal2));
            });

            var expectedHits = 1;
            var expectedMisses = 3;
            var expectedEjections = 0;
            var expectedSize = 3;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void CacheGetsClearedIfItGoesAboveCapacity()
        {
            var val1 = "value1";
            var val2 = "value2";
            var val3 = "value3";
            var val4 = "value4";
            var val5 = "value5";
            var val6 = "value6";

            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);

            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key3", () => val3);
            cache.GetOrAdd("key4", () => val4);
            cache.GetOrAdd("key5", () => val5);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.

            //This checks that the clearing didn't happen.
            Assert.That(cache.Size, Is.EqualTo(capacity));

            //Overflow the cache
            cache.GetOrAdd("key6", () => val6);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.

            //Checks the clearing happened.
            Assert.That(cache.Size, Is.EqualTo(0));

            var expectedHits = 0;
            var expectedMisses = 6;
            var expectedEjections = 6;
            var expectedSize = 0;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void AttemptSetCapacityOfZeroThrowsExceptionOnConstructor()
        {
            Assert.Catch(typeof(ArgumentOutOfRangeException), () => new SimpleCache<string, string>(0));
        }

        [Test]
        public void AttemptSetCapacityOfZeroThrowsExceptionOnSet()
        {
            var cache = new SimpleCache<string, string>(5);
            Assert.Catch(typeof(ArgumentOutOfRangeException), () => cache.Capacity = 0);
        }

        [Test]
        public void Capacity_MatchesWhatItWasSetTo()
        {
            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);

            Assert.That(cache.Capacity, Is.EqualTo(capacity));

            int newCapacity = 10;
            cache.Capacity = newCapacity;

            Assert.That(cache.Capacity, Is.EqualTo(10));
        }

        [Test]
        public void Capacity_Increase_CapacityIsActuallyIncreased()
        {
            var val1 = "value1";
            var val2 = "value2";
            var val3 = "value3";
            var val4 = "value4";
            var val5 = "value5";
            var val6 = "value6";
            var val7 = "value7";
            var val8 = "value8";

            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);

            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key3", () => val3);
            cache.GetOrAdd("key4", () => val4);
            cache.GetOrAdd("key5", () => val5);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.

            //This checks that the clearing didn't happen.
            Assert.That(cache.Size, Is.EqualTo(capacity));

            int newCapacity = 7;
            cache.Capacity = newCapacity;

            cache.GetOrAdd("key6", () => val6);
            cache.GetOrAdd("key7", () => val7);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.
            Assert.That(cache.Size, Is.EqualTo(newCapacity));

            //This should overflowt the cache
            cache.GetOrAdd("key8", () => val8);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.
            Assert.That(cache.Size, Is.EqualTo(0));


            var expectedHits = 0;
            var expectedMisses = 8;
            var expectedEjections = 8;
            var expectedSize = 0;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void Capacity_Decrease_CapacityIsActuallyDecrease()
        {
            var val1 = "value1";
            var val2 = "value2";
            var val3 = "value3";
            var val4 = "value4";
            var val5 = "value5";

            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);

            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key3", () => val3);
            cache.GetOrAdd("key4", () => val4);
            cache.GetOrAdd("key5", () => val5);

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.

            //This checks that the clearing didn't happen.
            Assert.That(cache.Size, Is.EqualTo(capacity));

            int newCapacity = 3;
            cache.Capacity = newCapacity;

            cache.MaintainCache(); // force cache to maintain, normally done on a timer.

            //This checks that the clearing happened and setting new capacity works.
            Assert.That(cache.Size, Is.EqualTo(0));

            var expectedHits = 0;
            var expectedMisses = 5;
            var expectedEjections = 5;
            var expectedSize = 0;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void StatsGetReset()
        {
            var val1 = "value1";
            var val2 = "value2";

            int capacity = 5;
            var cache = new SimpleCache<string, string>(capacity);

            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);
            cache.GetOrAdd("key1", () => val1);
            cache.GetOrAdd("key2", () => val2);

            var expectedHits = 2;
            var expectedMisses = 2;
            var expectedEjections = 0;
            var expectedSize = 2;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);

            cache.ResetStats();

            expectedHits = 0;
            expectedMisses = 0;
            expectedEjections = 0;
            expectedSize = 2;

            EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedSize);
        }

        [Test]
        public void CacheMaintenanceThreadMaintainsCache()
        {
            var cache = new SimpleCache<string, string>(1);
            cache.GetOrAdd("key1", () => "value1");
            cache.GetOrAdd("key2", () => "value2");

            Thread.Sleep(2500); // unnecessarily long, but should eliminate test flickers

            EvaluateCacheMetrics(cache, 0, 2, 2, 0);
        }

        private void EvaluateCacheMetrics<T, V>(SimpleCache<T, V> cache, int expectedHits, int expectedMisses,
            int expectedEjections, int expectedSize) where V : class
        {
            Assert.Multiple(() =>
            {
                Assert.That(cache.CountHits, Is.EqualTo(expectedHits));
                Assert.That(cache.CountMisses, Is.EqualTo(expectedMisses));
                Assert.That(cache.CountEjections, Is.EqualTo(expectedEjections));
                Assert.That(cache.Size, Is.EqualTo(expectedSize));
            });
        }

    }
}
