// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utilities;

[TestFixture]
public class ClockCacheTests
{
    private const int Capacity = 8;

    private int _factoryCalls;

    private sealed class Box
    {
    }

    [SetUp]
    public void SetUp()
    {
        _factoryCalls = 0;
    }

    private ClockCache<string, Box> CreateCache(int capacity = Capacity)
    {
        return new ClockCache<string, Box>(capacity, key =>
        {
            Interlocked.Increment(ref _factoryCalls);
            return new Box();
        }, StringComparer.Ordinal);
    }

    private static string Key(int index) => "k" + index;

    private ClockCache<string, Box> CreateFullCache(out Box[] originals)
    {
        var cache = CreateCache();
        originals = new Box[Capacity];
        for (var i = 0; i < Capacity; i++)
        {
            originals[i] = cache.GetOrAdd(Key(i));
        }

        return cache;
    }

    [Test]
    public void Hit_ReturnsSameInstance_FactoryCalledOnce()
    {
        var cache = CreateCache();

        var first = cache.GetOrAdd("a");
        var second = cache.GetOrAdd("a");

        Assert.That(second, Is.SameAs(first));
        Assert.That(_factoryCalls, Is.EqualTo(1));
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    [Test]
    public void Count_NeverExceedsCapacity()
    {
        var cache = CreateCache();

        for (var i = 0; i < 100; i++)
        {
            cache.GetOrAdd(Key(i));

            Assert.That(cache.Count, Is.EqualTo(Math.Min(i + 1, Capacity)));
        }
    }

    [Test]
    public void Flood_KeepsAllButOneOldKey()
    {
        var cache = CreateFullCache(out var originals);

        for (var i = 0; i < 80; i++)
        {
            cache.GetOrAdd("flood" + i);
        }

        Assert.That(cache.Count, Is.EqualTo(Capacity));
        var callsBeforeChecks = _factoryCalls;
        for (var i = 1; i < Capacity; i++)
        {
            Assert.That(cache.GetOrAdd(Key(i)), Is.SameAs(originals[i]));
        }

        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks));
        Assert.That(cache.GetOrAdd(Key(0)), Is.Not.SameAs(originals[0]));
        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks + 1));
    }

    [Test]
    public void Flood_DoesNotEvictHotOrColdOldKeys()
    {
        var cache = CreateFullCache(out var originals);
        for (var i = 1; i <= 3; i++)
        {
            cache.GetOrAdd(Key(i));
        }

        for (var i = 0; i < 80; i++)
        {
            cache.GetOrAdd("flood" + i);
        }

        var callsBeforeChecks = _factoryCalls;
        for (var i = 1; i < Capacity; i++)
        {
            Assert.That(cache.GetOrAdd(Key(i)), Is.SameAs(originals[i]));
        }

        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks));
    }

    [Test]
    public void NewKey_HitBeforeNextMiss_SurvivesThatMiss()
    {
        var cache = CreateFullCache(out _);
        var a = cache.GetOrAdd("A");
        cache.GetOrAdd("A");

        cache.GetOrAdd("B");

        var callsBeforeCheck = _factoryCalls;
        Assert.That(cache.GetOrAdd("A"), Is.SameAs(a));
        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeCheck));
        Assert.That(cache.Count, Is.EqualTo(Capacity));
    }

    [Test]
    public void NewKey_NoHitBeforeNextMiss_IsEvictedByIt()
    {
        var cache = CreateFullCache(out _);
        var a = cache.GetOrAdd("A");

        var b = cache.GetOrAdd("B");

        Assert.That(cache.Count, Is.EqualTo(Capacity));
        var callsBeforeChecks = _factoryCalls;
        Assert.That(cache.GetOrAdd("B"), Is.SameAs(b));
        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks));
        Assert.That(cache.GetOrAdd("A"), Is.Not.SameAs(a));
        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks + 1));
    }

    [Test]
    public void AllHot_Miss_EvictsExactlyOne()
    {
        var cache = CreateFullCache(out var originals);
        for (var i = 0; i < Capacity; i++)
        {
            cache.GetOrAdd(Key(i));
        }

        cache.GetOrAdd("X");

        Assert.That(cache.Count, Is.EqualTo(Capacity));
        var callsBeforeChecks = _factoryCalls;
        for (var i = 1; i < Capacity; i++)
        {
            Assert.That(cache.GetOrAdd(Key(i)), Is.SameAs(originals[i]));
        }

        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks));
        Assert.That(cache.GetOrAdd(Key(0)), Is.Not.SameAs(originals[0]));
        Assert.That(_factoryCalls, Is.EqualTo(callsBeforeChecks + 1));
    }

    [Test]
    public void FactoryThrows_PropagatesAndAddsNothing()
    {
        var shouldThrow = false;
        var cache = new ClockCache<string, Box>(Capacity, key =>
        {
            Interlocked.Increment(ref _factoryCalls);
            if (shouldThrow)
            {
                throw new InvalidOperationException("boom");
            }

            return new Box();
        }, StringComparer.Ordinal);
        cache.GetOrAdd("ok-before");
        var countBefore = cache.Count;
        shouldThrow = true;

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("a"));

        Assert.That(cache.Count, Is.EqualTo(countBefore));
        shouldThrow = false;
        var callsBefore = _factoryCalls;
        Assert.That(cache.GetOrAdd("a"), Is.Not.Null);
        Assert.That(_factoryCalls, Is.EqualTo(callsBefore + 1));
        Assert.That(cache.Count, Is.EqualTo(countBefore + 1));
    }

    [Test]
    public void Miss_KeyAddedDuringFactory_ReturnsExistingInstanceAndAddsNothingMore()
    {
        ClockCache<string, Box> cache = null;
        Box inner = null;
        var reentered = false;
        cache = new ClockCache<string, Box>(Capacity, key =>
        {
            if (!reentered)
            {
                reentered = true;
                inner = cache.GetOrAdd(key);
            }

            return new Box();
        }, StringComparer.Ordinal);

        var result = cache.GetOrAdd("a");

        Assert.That(result, Is.SameAs(inner));
        Assert.That(cache.Count, Is.EqualTo(1));
    }

    [Test]
    public void NullKey_Throws()
    {
        var cache = CreateCache();

        Assert.Throws<ArgumentNullException>(() => cache.GetOrAdd(null));
        Assert.That(_factoryCalls, Is.EqualTo(0));
    }

    [Test]
    public void Constructor_InvalidArguments_Throw()
    {
        Func<string, Box> factory = key => new Box();

        Assert.Throws<ArgumentOutOfRangeException>(() => new ClockCache<string, Box>(0, factory, StringComparer.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClockCache<string, Box>(-1, factory, StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>(() => new ClockCache<string, Box>(Capacity, null, StringComparer.Ordinal));
        Assert.Throws<ArgumentNullException>(() => new ClockCache<string, Box>(Capacity, factory, null));
    }

    [Test]
    public void Capacity_One_ReplacesTheOnlyEntry()
    {
        var cache = CreateCache(1);

        cache.GetOrAdd("a");
        cache.GetOrAdd("b");

        Assert.That(cache.Count, Is.EqualTo(1));
        Assert.That(_factoryCalls, Is.EqualTo(2));
        cache.GetOrAdd("b");
        Assert.That(_factoryCalls, Is.EqualTo(2));
        cache.GetOrAdd("a");
        Assert.That(_factoryCalls, Is.EqualTo(3));
    }

    [Test]
    public void SlotsGrow_PastInitialLength()
    {
        var cache = CreateCache(100);
        var instances = new Box[50];
        for (var i = 0; i < instances.Length; i++)
        {
            instances[i] = cache.GetOrAdd(Key(i));
        }

        Assert.That(cache.Count, Is.EqualTo(50));
        for (var i = 0; i < instances.Length; i++)
        {
            Assert.That(cache.GetOrAdd(Key(i)), Is.SameAs(instances[i]));
        }

        Assert.That(_factoryCalls, Is.EqualTo(50));
    }

    [Test]
    public void Eviction_WorksAtCapacityThatIsNotSixteenTimesPowerOfTwo()
    {
        var cache = CreateCache(100);

        for (var i = 0; i < 250; i++)
        {
            cache.GetOrAdd(Key(i));
        }

        Assert.That(cache.Count, Is.EqualTo(100));
    }

#if NET
    [Test]
    public void Hit_AllocatesZeroBytes()
    {
        var cache = CreateCache();
        cache.GetOrAdd("a");
        for (var i = 0; i < 100; i++)
        {
            cache.GetOrAdd("a");
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
        {
            cache.GetOrAdd("a");
        }

        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.That(after - before, Is.EqualTo(0));
    }
#endif

    [Test]
    public void Parallel_FewerKeysThanCapacity_OneInstancePerKey()
    {
        var cache = CreateCache(1000);
        var keys = new string[500];
        for (var i = 0; i < keys.Length; i++)
        {
            keys[i] = Key(i);
        }

        var firstInstances = new ConcurrentDictionary<string, Box>();
        var mismatches = 0;

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
        {
            for (var call = 0; call < 10000; call++)
            {
                var key = keys[(call + worker * 61) % keys.Length];
                var value = cache.GetOrAdd(key);
                if (!ReferenceEquals(firstInstances.GetOrAdd(key, value), value))
                {
                    Interlocked.Increment(ref mismatches);
                }
            }
        });

        Assert.That(mismatches, Is.EqualTo(0));
        Assert.That(cache.Count, Is.EqualTo(500));
    }

    [Test]
    public void Parallel_MoreKeysThanCapacity_CountStaysBounded()
    {
        var cache = CreateCache(100);

        Parallel.For(0, 8, new ParallelOptions { MaxDegreeOfParallelism = 8 }, worker =>
        {
            for (var i = worker; i < 10000; i += 8)
            {
                cache.GetOrAdd(Key(i));
                cache.GetOrAdd("hot" + (i % 10));
            }
        });

        Assert.That(cache.Count, Is.LessThanOrEqualTo(100));
        Assert.That(cache.Count, Is.GreaterThan(0));
    }
}
