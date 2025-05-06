// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Database;

[TestFixture]
public class CacheByDatastoreVendorTests
{
    private CacheByDatastoreVendor<string, string> _cache;

    [SetUp]
    public void SetUp()
    {
        _cache = new CacheByDatastoreVendor<string, string>("TestCache");
    }

    [Test]
    public void GetOrAdd_AddsNewItemToCache()
    {
        var result = _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value1");

        Assert.That(result, Is.EqualTo("value1"));
        Assert.That(_cache.Contains(DatastoreVendor.MongoDB, "key1"), Is.True);
    }

    [Test]
    public void GetOrAdd_ReturnsExistingItemFromCache()
    {
        _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value1");
        var result = _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value2");

        Assert.That(result, Is.EqualTo("value1"));
    }

    [Test]
    public void TryAdd_AddsNewItemToCache()
    {
        var result = _cache.TryAdd(DatastoreVendor.MongoDB, "key1", () => "value1");

        Assert.That(result, Is.True);
        Assert.That(_cache.Contains(DatastoreVendor.MongoDB, "key1"), Is.True);
    }

    [Test]
    public void TryAdd_DoesNotAddExistingItemToCache()
    {
        _cache.TryAdd(DatastoreVendor.MongoDB, "key1", () => "value1");
        var result = _cache.TryAdd(DatastoreVendor.MongoDB, "key1", () => "value2");

        Assert.That(result, Is.False);
        Assert.That(_cache.Contains(DatastoreVendor.MongoDB, "key1"), Is.True);
    }

    [Test]
    public void Contains_ReturnsTrueForExistingItem()
    {
        _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value1");

        var result = _cache.Contains(DatastoreVendor.MongoDB, "key1");

        Assert.That(result, Is.True);
    }

    [Test]
    public void Contains_ReturnsFalseForNonExistingItem()
    {
        var result = _cache.Contains(DatastoreVendor.MongoDB, "key1");

        Assert.That(result, Is.False);
    }

    [Test]
    public void SetCapacity_UpdatesCacheCapacity()
    {
        _cache.SetCapacity(500);

        var result = _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value1");

        Assert.That(result, Is.EqualTo("value1"));
        Assert.That(_cache.Contains(DatastoreVendor.MongoDB, "key1"), Is.True);
    }

    [Test]
    public void Reset_ClearsAllCaches()
    {
        _cache.GetOrAdd(DatastoreVendor.MongoDB, "key1", () => "value1");
        _cache.GetOrAdd(DatastoreVendor.MySQL, "key2", () => "value2");

        _cache.Reset();

        Assert.That(_cache.Contains(DatastoreVendor.MongoDB, "key1"), Is.False);
        Assert.That(_cache.Contains(DatastoreVendor.MySQL, "key2"), Is.False);
    }
}
