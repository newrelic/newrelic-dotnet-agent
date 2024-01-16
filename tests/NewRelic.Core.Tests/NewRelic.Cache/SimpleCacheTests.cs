// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading;
using NewRelic.Core.Caching;

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

            //Use AreSame to ensure that we are getting a reference match.
            ClassicAssert.AreSame(val1, cache.Peek("key1"));
            ClassicAssert.AreSame(val2, cache.Peek("key2"));
            ClassicAssert.AreSame(val3, cache.Peek("key3"));

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

            //Use AreSame to ensure that we are getting a reference match.
            ClassicAssert.AreSame(val1, cache.Peek("key1"));
            ClassicAssert.AreSame(val2, cache.Peek("key2"));
            ClassicAssert.AreSame(val3, cache.Peek("key3"));

            ClassicAssert.AreSame(shouldbeVal2, val2);

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

            Thread.Sleep(1000); //allow the cache to check it's size.

            //This checks that the clearing didn't happen.
            ClassicAssert.AreEqual(capacity, cache.Size);

            //Overflow the cache
            cache.GetOrAdd("key6", () => val6);

            Thread.Sleep(1000); //allow the cache to check it's size.

            //Checks the clearing happened.
            ClassicAssert.AreEqual(0, cache.Size);

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

            ClassicAssert.AreEqual(capacity, cache.Capacity);

            int newCapacity = 10;
            cache.Capacity = newCapacity;

            ClassicAssert.AreEqual(10, cache.Capacity);
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

            Thread.Sleep(1000); //allow the cache to check it's size.

            //This checks that the clearing didn't happen.
            ClassicAssert.AreEqual(capacity, cache.Size);

            int newCapacity = 7;
            cache.Capacity = newCapacity;

            cache.GetOrAdd("key6", () => val6);
            cache.GetOrAdd("key7", () => val7);

            Thread.Sleep(1000); //allow the cache to check it's size.
            ClassicAssert.AreEqual(newCapacity, cache.Size);

            //This should overflowt the cache
            cache.GetOrAdd("key8", () => val8);

            Thread.Sleep(1000); //allow the cache to check it's size.
            ClassicAssert.AreEqual(0, cache.Size);


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

            Thread.Sleep(1000); //allow the cache to check it's size.

            //This checks that the clearing didn't happen.
            ClassicAssert.AreEqual(capacity, cache.Size);

            int newCapacity = 3;
            cache.Capacity = newCapacity;

            Thread.Sleep(1000); //allow the cache to check it's size.

            //This checks that the clearing happened and setting new capacity works.
            ClassicAssert.AreEqual(0, cache.Size);

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

        private void EvaluateCacheMetrics<T, V>(SimpleCache<T, V> cache, int expectedHits, int expectedMisses,
            int expectedEjections, int expectedSize) where V : class
        {
            ClassicAssert.AreEqual(expectedHits, cache.CountHits);
            ClassicAssert.AreEqual(expectedMisses, cache.CountMisses);
            ClassicAssert.AreEqual(expectedEjections, cache.CountEjections);
            ClassicAssert.AreEqual(expectedSize, cache.Size);
        }

    }
}
