// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Attributes.Tests;

[TestFixture]
public class FlushingAttributeCacheTests
{
    private int _factoryCalls;
    private int _flushes;

    [SetUp]
    public void SetUp()
    {
        _factoryCalls = 0;
        _flushes = 0;
    }

    private FlushingAttributeCache<object> CreateCache(int capacity)
    {
        return new FlushingAttributeCache<object>("Test", capacity, key => { _factoryCalls++; return new object(); }, () => _flushes++);
    }

    [Test]
    public void GetOrAdd_NewKey_CallsFactoryOnceAndCaches()
    {
        var cache = CreateCache(10);

        var first = cache.GetOrAdd("a");
        var second = cache.GetOrAdd("a");

        Assert.Multiple(() =>
        {
            Assert.That(second, Is.SameAs(first));
            Assert.That(_factoryCalls, Is.EqualTo(1));
            Assert.That(cache.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void GetOrAdd_OverCapacity_FlushesThenAddsNewKey()
    {
        var cache = CreateCache(3);

        var a = cache.GetOrAdd("a");
        cache.GetOrAdd("b");
        cache.GetOrAdd("c");
        var d = cache.GetOrAdd("d");

        Assert.Multiple(() =>
        {
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(_flushes, Is.EqualTo(1));
            Assert.That(cache.GetOrAdd("d"), Is.SameAs(d));
            Assert.That(cache.GetOrAdd("a"), Is.Not.SameAs(a));
        });
    }

    [Test]
    public void GetOrAdd_ManyNewKeys_EntryCountNeverExceedsCapacity()
    {
        const int capacity = 10;
        var cache = CreateCache(capacity);

        for (var i = 0; i < 1000; i++)
        {
            cache.GetOrAdd("key" + i);
            Assert.That(cache.Count, Is.LessThanOrEqualTo(capacity));
        }
    }

    [Test]
    public void GetOrAdd_CalledFromInsideFlush_ReturnsValueWithoutCaching()
    {
        FlushingAttributeCache<object> cache = null;
        object innerValue = null;
        var countInsideFlush = -1;
        cache = new FlushingAttributeCache<object>("Test", 3, key => new object(), () =>
        {
            innerValue = cache.GetOrAdd("x");
            countInsideFlush = cache.Count;
        });

        cache.GetOrAdd("a");
        cache.GetOrAdd("b");
        cache.GetOrAdd("c");
        cache.GetOrAdd("d");

        Assert.Multiple(() =>
        {
            Assert.That(innerValue, Is.Not.Null);
            Assert.That(countInsideFlush, Is.EqualTo(0));
        });
    }

    [Test]
    public void GetOrAdd_FlushCallbackThrows_ExceptionSurfacesAndCachingResumes()
    {
        var thrown = false;
        var cache = new FlushingAttributeCache<object>("Test", 3, key => new object(), () =>
        {
            if (!thrown)
            {
                thrown = true;
                throw new InvalidOperationException("flush callback failed");
            }
        });

        cache.GetOrAdd("a");
        cache.GetOrAdd("b");
        cache.GetOrAdd("c");

        Assert.Throws<InvalidOperationException>(() => cache.GetOrAdd("d"));

        var e = cache.GetOrAdd("e");

        Assert.Multiple(() =>
        {
            Assert.That(e, Is.Not.Null);
            Assert.That(cache.Count, Is.EqualTo(1));
            Assert.That(cache.GetOrAdd("e"), Is.SameAs(e));
        });
    }

    [Test]
    public void GetOrAdd_Flushes_LogOnceAndCallbackEachTime()
    {
        const string logText = "The Test attribute cache reached its limit of 1 names";

        using (var logging = new TestUtilities.Logging())
        {
            var cache = CreateCache(1);

            cache.GetOrAdd("a");
            Assert.That(logging.HasMessageThatContains(logText), Is.False);

            cache.GetOrAdd("b");
            cache.GetOrAdd("c");

            Assert.Multiple(() =>
            {
                Assert.That(_flushes, Is.EqualTo(2));
                Assert.That(logging.InfoCount, Is.EqualTo(1));
                Assert.That(logging.HasMessageThatContains(logText), Is.True);
            });
        }
    }
}
