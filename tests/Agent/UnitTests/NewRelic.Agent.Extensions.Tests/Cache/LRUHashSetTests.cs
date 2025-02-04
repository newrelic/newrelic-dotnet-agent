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
    public class LRUHashSetTests
    {
        [Test]
        public void Constructor_ShouldThrowException_WhenCapacityIsZeroOrNegative()
        {
            Assert.Throws<ArgumentException>(() => new LRUHashSet<int>(0));
            Assert.Throws<ArgumentException>(() => new LRUHashSet<int>(-1));
        }

        [Test]
        public void Add_ShouldAddItemToSet()
        {
            var set = new LRUHashSet<int>(2);
            var added = set.Add(1);
            Assert.That(added, Is.True);
            Assert.That(set.Contains(1), Is.True);
        }

        [Test]
        public void Add_ShouldNotAddDuplicateItem()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            var added = set.Add(1);
            Assert.That(added, Is.False);
            Assert.That(set.Contains(1), Is.True);
        }

        [Test]
        public void Add_ShouldEvictLeastRecentlyUsedItem_WhenCapacityIsExceeded()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            set.Add(2);
            set.Add(3);
            Assert.That(set.Contains(1), Is.False);
            Assert.That(set.Contains(2), Is.True);
            Assert.That(set.Contains(3), Is.True);
        }

        [Test]
        public void Contains_ShouldReturnTrueForExistingItem()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            var contains = set.Contains(1);
            Assert.That(contains, Is.True);
        }

        [Test]
        public void Contains_ShouldReturnFalseForNonExistingItem()
        {
            var set = new LRUHashSet<int>(2);
            var contains = set.Contains(1);
            Assert.That(contains, Is.False);
        }

        [Test]
        public void Remove_ShouldRemoveItemFromSet()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            var removed = set.Remove(1);
            Assert.That(removed, Is.True);
            Assert.That(set.Contains(1), Is.False);
        }

        [Test]
        public void Remove_ShouldReturnFalseForNonExistingItem()
        {
            var set = new LRUHashSet<int>(2);
            var removed = set.Remove(1);
            Assert.That(removed, Is.False);
        }

        [Test]
        public void Count_ShouldReturnNumberOfItemsInSet()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            set.Add(2);
            var count = set.Count;
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void Set_ShouldBeThreadSafe()
        {
            var set = new LRUHashSet<int>(100);
            var addTasks = new List<Task>();
            var containsTasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                int index = i;
                addTasks.Add(Task.Run(() => set.Add(index)));
            }

            Task.WaitAll(addTasks.ToArray());

            for (int i = 0; i < 100; i++)
            {
                int index = i;
                containsTasks.Add(Task.Run(() => set.Contains(index)));
            }

            Task.WaitAll(containsTasks.ToArray());

            for (int i = 0; i < 100; i++)
            {
                Assert.That(set.Contains(i), Is.True);
            }
        }

        [Test]
        public void Add_ShouldHandleNullValues()
        {
            var set = new LRUHashSet<string>(2);
            var added = set.Add(null);
            Assert.That(added, Is.True);
            Assert.That(set.Contains(null), Is.True);
        }

        [Test]
        public void Add_ShouldHandleCapacityOfOne()
        {
            var set = new LRUHashSet<int>(1);
            set.Add(1);
            set.Add(2);
            Assert.That(set.Contains(1), Is.False);
            Assert.That(set.Contains(2), Is.True);
        }

        [Test]
        public void Add_ShouldMoveAccessedItemToFront()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            set.Add(2);
            set.Add(1);
            set.Add(3);
            Assert.That(set.Contains(1), Is.True);
            Assert.That(set.Contains(2), Is.False);
            Assert.That(set.Contains(3), Is.True);
        }

        [Test]
        public void ConcurrentAddAndRemove_ShouldBeThreadSafe()
        {
            var set = new LRUHashSet<int>(100);
            var addTasks = new List<Task>();
            var removeTasks = new List<Task>();

            // Add items concurrently
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                addTasks.Add(Task.Run(() => set.Add(index)));
            }

            Task.WaitAll(addTasks.ToArray());

            // Remove items concurrently
            for (int i = 0; i < 100; i++)
            {
                int index = i;
                removeTasks.Add(Task.Run(() => set.Remove(index)));
            }

            Task.WaitAll(removeTasks.ToArray());

            // Verify that all items have been removed
            for (int i = 0; i < 100; i++)
            {
                Assert.That(set.Contains(i), Is.False);
            }
        }

        [Test]
        public void Add_ShouldHandleLargeNumberOfItems()
        {
            var set = new LRUHashSet<int>(1000);
            for (int i = 0; i < 1000; i++)
            {
                set.Add(i);
            }
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(set.Contains(i), Is.True);
            }
        }

        [Test]
        public void Add_ShouldHandleDuplicateAdditions()
        {
            var set = new LRUHashSet<int>(2);
            set.Add(1);
            set.Add(1);
            Assert.That(set.Count, Is.EqualTo(1));
        }

        [Test]
        public void Remove_ShouldHandleNonExistentItem()
        {
            var set = new LRUHashSet<int>(2);
            var removed = set.Remove(1);
            Assert.That(removed, Is.False);
        }

        [Test]
        public void Add_ShouldHandleMaxCapacity()
        {
            var set = new LRUHashSet<int>(int.MaxValue);
            set.Add(1);
            Assert.That(set.Contains(1), Is.True);
        }
    }
}
