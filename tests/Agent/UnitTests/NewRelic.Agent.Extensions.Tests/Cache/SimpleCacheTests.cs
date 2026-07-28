// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Extensions.Caching;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Cache;

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
        var expectedDropped = 0;
        var expectedSize = 3;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);
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
        var expectedDropped = 0;
        var expectedSize = 3;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);
    }

    [Test]
    public void GetOrAdd_DropsNewItem_ButStillReturnsIt_WhenCacheAtCapacity()
    {
        int capacity = 2;
        // Disable the internal timer: the assertion below expects the drop to NOT have been cleared
        // yet, which a live 500ms timer could race.
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");

        //Cache is now at capacity; a new key should be dropped, not cached, but the computed value is still returned.
        var val3 = cache.GetOrAdd("key3", () => "value3");

        Assert.Multiple(() =>
        {
            Assert.That(val3, Is.EqualTo("value3"));
            Assert.That(cache.Peek("key3"), Is.Null);
            Assert.That(cache.Size, Is.EqualTo(capacity));
            Assert.That(cache.CountDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryAdd_DropsNewItem_WhenCacheAtCapacity()
    {
        int capacity = 2;
        // Disable the internal timer: see comment in GetOrAdd_DropsNewItem_ButStillReturnsIt_WhenCacheAtCapacity.
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

        cache.TryAdd("key1", () => "value1");
        cache.TryAdd("key2", () => "value2");

        var result = cache.TryAdd("key3", () => "value3");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(cache.Peek("key3"), Is.Null);
            Assert.That(cache.Size, Is.EqualTo(capacity));
            Assert.That(cache.CountDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryAdd_ReturnsFalse_WhenKeyAlreadyExists_EvenAtCapacity()
    {
        int capacity = 2;
        var cache = new SimpleCache<string, string>(capacity);

        var val1 = "value1";
        cache.TryAdd("key1", () => val1);
        cache.TryAdd("key2", () => "value2");

        //key1 already exists; this is not a capacity drop.
        var result = cache.TryAdd("key1", () => "somethingElse");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(cache.Peek("key1"), Is.SameAs(val1));
            Assert.That(cache.CountDropped, Is.EqualTo(0));
        });
    }

    [Test]
    public void MaintainCache_PersistsCache_WhenAtCapacityButNothingWasDropped()
    {
        int capacity = 2;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.

        Assert.Multiple(() =>
        {
            Assert.That(cache.Size, Is.EqualTo(capacity));
            Assert.That(cache.CountDropped, Is.EqualTo(0));
            Assert.That(cache.CountEjections, Is.EqualTo(0));
        });
    }

    [Test]
    public void MaintainCache_ClearsCache_WhenItemsWereDropped()
    {
        int capacity = 2;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");

        //Overflow the cache so an item gets dropped.
        cache.GetOrAdd("key3", () => "value3");

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.

        Assert.Multiple(() =>
        {
            Assert.That(cache.Size, Is.EqualTo(0));
            Assert.That(cache.CountEjections, Is.EqualTo(capacity));
            Assert.That(cache.CountDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void CacheGetsClearedOnlyAfterAnItemIsDropped()
    {
        var val1 = "value1";
        var val2 = "value2";
        var val3 = "value3";
        var val4 = "value4";
        var val5 = "value5";
        var val6 = "value6";

        int capacity = 5;
        // Disable the internal timer: see comment in GetOrAdd_DropsNewItem_ButStillReturnsIt_WhenCacheAtCapacity.
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

        cache.GetOrAdd("key1", () => val1);
        cache.GetOrAdd("key2", () => val2);
        cache.GetOrAdd("key3", () => val3);
        cache.GetOrAdd("key4", () => val4);
        cache.GetOrAdd("key5", () => val5);

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.

        //This checks that filling the cache exactly to capacity did not drop anything, so nothing was cleared.
        Assert.That(cache.Size, Is.EqualTo(capacity));

        //Overflow the cache; the new key is dropped rather than added.
        cache.GetOrAdd("key6", () => val6);

        Assert.That(cache.Peek("key6"), Is.Null);
        Assert.That(cache.Size, Is.EqualTo(capacity));

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.

        //Checks the clearing happened because an item was dropped.
        Assert.That(cache.Size, Is.EqualTo(0));

        var expectedHits = 0;
        var expectedMisses = 6;
        var expectedEjections = 5;
        var expectedDropped = 1;
        var expectedSize = 0;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);
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

        //This should overflow the cache; key8 is dropped rather than added.
        cache.GetOrAdd("key8", () => val8);

        Assert.That(cache.Peek("key8"), Is.Null);

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.
        Assert.That(cache.Size, Is.EqualTo(0));

        var expectedHits = 0;
        var expectedMisses = 8;
        var expectedEjections = 7;
        var expectedDropped = 1;
        var expectedSize = 0;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);
    }

    [Test]
    public void Capacity_Decrease_ClearsCacheOnNextMaintenance_EvenWithoutADrop()
    {
        var val1 = "value1";
        var val2 = "value2";
        var val3 = "value3";
        var val4 = "value4";
        var val5 = "value5";

        int capacity = 5;
        // Disable the internal timer: see comment in GetOrAdd_DropsNewItem_ButStillReturnsIt_WhenCacheAtCapacity.
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

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

        //Lowering capacity below the current size is picked up on the very next maintenance tick even
        //though nothing was dropped - a capacity reduction (e.g. for memory pressure) must not wait on
        //a new distinct key showing up to reclaim the over-capacity entries.
        cache.MaintainCache();

        Assert.Multiple(() =>
        {
            Assert.That(cache.Size, Is.EqualTo(0));
            Assert.That(cache.CountEjections, Is.EqualTo(capacity));
            Assert.That(cache.CountDropped, Is.EqualTo(0));
        });
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
        var expectedDropped = 0;
        var expectedSize = 2;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);

        cache.ResetStats();

        expectedHits = 0;
        expectedMisses = 0;
        expectedEjections = 0;
        expectedDropped = 0;
        expectedSize = 2;

        EvaluateCacheMetrics(cache, expectedHits, expectedMisses, expectedEjections, expectedDropped, expectedSize);
    }

    [Test]
    public void ResetStats_ResetsDroppedCounter()
    {
        int capacity = 1;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2"); // dropped, cache is at capacity

        Assert.That(cache.CountDropped, Is.EqualTo(1));

        cache.ResetStats();

        Assert.That(cache.CountDropped, Is.EqualTo(0));
    }

    [Test]
    public void CacheMaintenanceThreadMaintainsCache()
    {
        var cache = new SimpleCache<string, string>(1);
        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2"); // dropped, cache is at capacity

        Thread.Sleep(2500); // unnecessarily long, but should eliminate test flickers

        EvaluateCacheMetrics(cache, 0, 2, 1, 1, 0);
    }

    [Test]
    public void TryAdd_AddsNewItemToCache()
    {
        var val1 = "value1";
        int capacity = 5;
        var cache = new SimpleCache<string, string>(capacity);

        var result = cache.TryAdd("key1", () => val1);

        Assert.That(result, Is.True);
        Assert.That(cache.Peek("key1"), Is.SameAs(val1));
    }

    [Test]
    public void TryAdd_DoesNotAddExistingItemToCache()
    {
        var val1 = "value1";
        var val2 = "value2";
        int capacity = 5;
        var cache = new SimpleCache<string, string>(capacity);

        cache.TryAdd("key1", () => val1);
        var result = cache.TryAdd("key1", () => val2);

        Assert.That(result, Is.False);
        Assert.That(cache.Peek("key1"), Is.SameAs(val1));
    }

    [Test]
    public void TryAdd_DoesNotInvokeValueFunc_WhenKeyAlreadyExists()
    {
        int capacity = 5;
        var cache = new SimpleCache<string, string>(capacity);

        cache.TryAdd("key1", () => "value1");

        var valueFuncInvocations = 0;
        var result = cache.TryAdd("key1", () =>
        {
            valueFuncInvocations++;
            return "value2";
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            //The key is already cached, so there is no reason to build a value for it.
            Assert.That(valueFuncInvocations, Is.EqualTo(0));
            Assert.That(cache.CountDropped, Is.EqualTo(0));
        });
    }

    [Test]
    public void TryAdd_DoesNotInvokeValueFunc_WhenCacheAtCapacity()
    {
        int capacity = 1;
        var cache = new SimpleCache<string, string>(capacity);

        cache.TryAdd("key1", () => "value1");

        var valueFuncInvocations = 0;
        var result = cache.TryAdd("key2", () =>
        {
            valueFuncInvocations++;
            return "value2";
        });

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(valueFuncInvocations, Is.EqualTo(0));
            Assert.That(cache.CountDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void Dispose_ClearsCache()
    {
        var val1 = "value1";
        int capacity = 5;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => val1);
        cache.Dispose();

        Assert.That(cache.Size, Is.EqualTo(0));
        Assert.That(cache.Peek("key1"), Is.Null);
    }

    [Test]
    public void Reset_ClearsCacheAndStats()
    {
        int capacity = 2;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key1", () => "value1"); // hit
        cache.GetOrAdd("key2", () => "value2");
        cache.GetOrAdd("key3", () => "value3"); // dropped, cache is at capacity

        cache.Reset();

        EvaluateCacheMetrics(cache, 0, 0, 0, 0, 0);
        Assert.That(cache.Peek("key1"), Is.Null);
    }

    [Test]
    public void Size_CountsOnlyActualInsertions()
    {
        int capacity = 10;
        var cache = new SimpleCache<string, string>(capacity);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");
        cache.GetOrAdd("key2", () => "value2"); // already cached, not a new entry
        cache.TryAdd("key3", () => "value3");
        cache.TryAdd("key3", () => "value3"); // already cached, not a new entry
        cache.TryAdd("key1", () => "value1"); // already cached by GetOrAdd, not a new entry

        Assert.Multiple(() =>
        {
            Assert.That(cache.Size, Is.EqualTo(3));
            Assert.That(cache.CountDropped, Is.EqualTo(0));
            Assert.That(cache.CountEjections, Is.EqualTo(0));
        });
    }

    [Test]
    public void MaintainCache_ResetsSize_SoTheClearedCacheAcceptsNewItemsAgain()
    {
        int capacity = 2;
        // Disable the internal timer: see comment in GetOrAdd_DropsNewItem_ButStillReturnsIt_WhenCacheAtCapacity.
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");
        cache.GetOrAdd("key3", () => "value3"); // dropped, cache is at capacity

        cache.MaintainCache(); // force cache to maintain, normally done on a timer.
        Assert.That(cache.Size, Is.EqualTo(0));

        //The cache is empty, so this must be cached rather than dropped.
        cache.GetOrAdd("key4", () => "value4");

        Assert.Multiple(() =>
        {
            Assert.That(cache.Peek("key4"), Is.EqualTo("value4"));
            Assert.That(cache.Size, Is.EqualTo(1));
            Assert.That(cache.CountDropped, Is.EqualTo(1));
        });
    }

    [Test]
    public void MaintainCache_DoesNotKeepClearing_OnLaterTicksAfterASingleDropIsResolved()
    {
        // Regression test: _countDropped (cumulative, only reset via ResetStats) used to be the
        // maintenance trigger, so a single drop caused every future tick to clear the cache forever,
        // permanently degrading it to a 500ms-lifetime cache. _droppedSinceLastMaintenance fixes this
        // by being reset every tick.
        int capacity = 2;
        var cache = new SimpleCache<string, string>(capacity, Timeout.Infinite);

        cache.GetOrAdd("key1", () => "value1");
        cache.GetOrAdd("key2", () => "value2");
        cache.GetOrAdd("key3", () => "value3"); // dropped, cache is at capacity

        cache.MaintainCache(); // clears because of the drop
        Assert.That(cache.Size, Is.EqualTo(0));

        cache.GetOrAdd("key4", () => "value4"); // cache is empty, so this is cached, not dropped

        //Nothing new was dropped and the cache is within capacity, so repeated ticks must not clear it.
        cache.MaintainCache();
        cache.MaintainCache();

        Assert.Multiple(() =>
        {
            Assert.That(cache.Peek("key4"), Is.EqualTo("value4"));
            Assert.That(cache.Size, Is.EqualTo(1));
            Assert.That(cache.CountEjections, Is.EqualTo(capacity));
        });
    }

    [Test]
    public void GetOrAdd_ConcurrentCallsForSameNewKey_CountTheInsertOnlyOnce()
    {
        const int threadCount = 16;
        int capacity = 10;
        var cache = new SimpleCache<string, object>(capacity);

        var results = new object[threadCount];
        var startGate = new ManualResetEventSlim(false);
        var threads = new Thread[threadCount];

        for (var i = 0; i < threadCount; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                startGate.Wait();
                //Each thread offers a distinct instance, so the winner of the race is identifiable.
                results[index] = cache.GetOrAdd("sameKey", () => new object());
            });
            threads[i].Start();
        }

        startGate.Set();

        foreach (var thread in threads)
        {
            Assert.That(thread.Join(TimeSpan.FromSeconds(30)), Is.True, "Cache access thread did not complete.");
        }

        Assert.Multiple(() =>
        {
            //Only one entry was actually inserted no matter how many threads raced for the key, so
            //the tracked size must be exactly 1. Counting inside a factory delegate handed to
            //ConcurrentDictionary.GetOrAdd would inflate this, because that delegate can run on more
            //than one thread even though only one of its results is stored.
            Assert.That(cache.Size, Is.EqualTo(1));
            Assert.That(cache.CountDropped, Is.EqualTo(0));

            //Threads that lost the race must return the value that is actually cached.
            Assert.That(results, Is.All.SameAs(cache.Peek("sameKey")));
        });
    }


    private void EvaluateCacheMetrics<T, V>(SimpleCache<T, V> cache, int expectedHits, int expectedMisses,
        int expectedEjections, int expectedDropped, int expectedSize) where V : class
    {
        Assert.Multiple(() =>
        {
            Assert.That(cache.CountHits, Is.EqualTo(expectedHits));
            Assert.That(cache.CountMisses, Is.EqualTo(expectedMisses));
            Assert.That(cache.CountEjections, Is.EqualTo(expectedEjections));
            Assert.That(cache.CountDropped, Is.EqualTo(expectedDropped));
            Assert.That(cache.Size, Is.EqualTo(expectedSize));
        });
    }

}
