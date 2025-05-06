// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using NewRelic.Agent.Extensions.Caching;

namespace Agent.Extensions.Tests.Cache
{
    [TestFixture]
    public class LRUCacheTests
    {
        [Test]
        public void Constructor_ShouldThrowException_WhenCapacityIsZeroOrNegative()
        {
            Assert.Throws<ArgumentException>(() => new LRUCache<int, string>(0));
            Assert.Throws<ArgumentException>(() => new LRUCache<int, string>(-1));
        }

        [Test]
        public void Put_ShouldAddItemToCache()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);

            // Act
            cache.Put(1, "one");

            // Assert
            Assert.That(cache.ContainsKey(1), Is.True);
            Assert.That(cache.Get(1), Is.EqualTo("one"));
        }

        [Test]
        public void Get_ShouldThrowException_WhenKeyNotFound()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => cache.Get(1));
        }

        [Test]
        public void Put_ShouldEvictLeastRecentlyUsedItem_WhenCapacityIsExceeded()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);
            cache.Put(1, "one");
            cache.Put(2, "two");

            // Act
            cache.Put(3, "three");

            // Assert
            Assert.That(cache.ContainsKey(1), Is.False);
            Assert.That(cache.ContainsKey(2), Is.True);
            Assert.That(cache.ContainsKey(3), Is.True);
        }

        [Test]
        public void Get_ShouldMoveAccessedItemToFront()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);
            cache.Put(1, "one");
            cache.Put(2, "two");

            // Act
            var value = cache.Get(1);
            cache.Put(3, "three");

            // Assert
            Assert.That(cache.ContainsKey(1), Is.True);
            Assert.That(cache.ContainsKey(2), Is.False);
            Assert.That(cache.ContainsKey(3), Is.True);
        }

        [Test]
        public void Put_ShouldUpdateValue_WhenKeyAlreadyExists()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);
            cache.Put(1, "one");

            // Act
            cache.Put(1, "uno");

            // Assert
            Assert.That(cache.Get(1), Is.EqualTo("uno"));
        }

        [Test]
        public void ContainsKey_ShouldReturnTrueForExistingKey()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);
            cache.Put(1, "one");

            // Act
            var containsKey = cache.ContainsKey(1);

            // Assert
            Assert.That(containsKey, Is.True);
        }

        [Test]
        public void ContainsKey_ShouldReturnFalseForNonExistingKey()
        {
            // Arrange
            var cache = new LRUCache<int, string>(2);

            // Act
            var containsKey = cache.ContainsKey(1);

            // Assert
            Assert.That(containsKey, Is.False);
        }

        [Test]
        public void Put_ShouldHandleEdgeCaseForCapacity()
        {
            // Arrange
            var cache = new LRUCache<int, string>(1);
            cache.Put(1, "one");

            // Act
            cache.Put(2, "two");

            // Assert
            Assert.That(cache.ContainsKey(1), Is.False);
            Assert.That(cache.ContainsKey(2), Is.True);
        }

        [Test]
        public void Cache_ShouldBeThreadSafe()
        {
            // Arrange
            var cache = new LRUCache<int, string>(100);
            var putTasks = new List<Task>();
            var getTasks = new List<Task>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                putTasks.Add(Task.Run(() => cache.Put(index, $"value{index}")));
            }

            Task.WaitAll(putTasks.ToArray());

            for (int i = 0; i < 100; i++)
            {
                int index = i;
                getTasks.Add(Task.Run(() => cache.Get(index)));
            }

            Task.WaitAll(getTasks.ToArray());

            // Assert
            for (int i = 0; i < 100; i++)
            {
                Assert.That(cache.ContainsKey(i), Is.True);
                Assert.That(cache.Get(i), Is.EqualTo($"value{i}"));
            }
        }
    }
}
