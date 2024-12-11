// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading.Tasks;
using NewRelic.Agent.Extensions.Caching;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Cache
{
    [TestFixture]
    public class WeakReferenceKeyTests
    {
        [Test]
        public void Constructor_ShouldInitializeWeakReference()
        {
            // Arrange
            var foo = new Foo();

            // Act
            var weakReferenceKey = new WeakReferenceKey<Foo>(foo);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(weakReferenceKey, Is.Not.Null);
                Assert.That(weakReferenceKey.Value, Is.Not.Null);
                Assert.That(weakReferenceKey.Value, Is.SameAs(foo));
            });
        }

        [Test]
        public void Equals_ShouldReturnTrueForSameObject()
        {
            // Arrange
            var foo = new Foo();
            var weakReferenceKey1 = new WeakReferenceKey<Foo>(foo);
            var weakReferenceKey2 = new WeakReferenceKey<Foo>(foo);

            // Act
            var result = weakReferenceKey1.Equals(weakReferenceKey2);

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void Equals_ShouldReturnFalseForDifferentObjects()
        {
            // Arrange
            var foo1 = new Foo();
            var foo2 = new Foo();
            var weakReferenceKey1 = new WeakReferenceKey<Foo>(foo1);
            var weakReferenceKey2 = new WeakReferenceKey<Foo>(foo2);

            // Act
            var result = weakReferenceKey1.Equals(weakReferenceKey2);

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void GetHashCode_ShouldReturnSameHashCodeForSameObject()
        {
            // Arrange
            var foo = new Foo();
            var weakReferenceKey1 = new WeakReferenceKey<Foo>(foo);
            var weakReferenceKey2 = new WeakReferenceKey<Foo>(foo);

            // Act
            var hashCode1 = weakReferenceKey1.GetHashCode();
            var hashCode2 = weakReferenceKey2.GetHashCode();

            // Assert
            Assert.That(hashCode1, Is.EqualTo(hashCode2));
        }

        [Test]
        public void GetHashCode_ShouldReturnDifferentHashCodeForDifferentObjects()
        {
            // Arrange
            var foo1 = new Foo();
            var foo2 = new Foo();
            var weakReferenceKey1 = new WeakReferenceKey<Foo>(foo1);
            var weakReferenceKey2 = new WeakReferenceKey<Foo>(foo2);

            // Act
            var hashCode1 = weakReferenceKey1.GetHashCode();
            var hashCode2 = weakReferenceKey2.GetHashCode();

            // Assert
            Assert.That(hashCode1, Is.Not.EqualTo(hashCode2));
        }

        [Test]
        public async Task GetHashCode_ShouldReturnZeroIfTargetIsGarbageCollected()
        {
            // Arrange
            var weakRefKey = GetWeakReferenceKey();

            // Act
            Assert.That(weakRefKey.Value, Is.Not.Null);
            // force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);
            GC.Collect(); // Force another collection

            // Assert
            Assert.That(weakRefKey.GetHashCode(), Is.EqualTo(0));
        }

        [Test]
        public async Task Value_ShouldReturnNullIfTargetIsGarbageCollected()
        {
            // Arrange
            var weakRefKey = GetWeakReferenceKey();

            // Act
            Assert.That(weakRefKey.Value, Is.Not.Null);
            // force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);
            GC.Collect(); // Force another collection

            // Assert
            Assert.That(weakRefKey.Value, Is.Null);
        }

        private WeakReferenceKey<Foo> GetWeakReferenceKey()
        {
            var foo = new Foo();
            return new WeakReferenceKey<Foo>(foo);
        }
        [Test]
        public void Equals_ShouldReturnFalseForNonWeakReferenceKeyObject()
        {
            // Arrange
            var foo = new Foo();
            var weakReferenceKey = new WeakReferenceKey<Foo>(foo);

            // Act
            var result = weakReferenceKey.Equals(new object());

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public async Task Equals_ShouldReturnFalseIfTargetIsGarbageCollected()
        {
            // Arrange
            var weakRefKey1 = GetWeakReferenceKey();
            var weakRefKey2 = GetWeakReferenceKey();

            // Act
            Assert.That(weakRefKey1.Value, Is.Not.Null);
            Assert.That(weakRefKey2.Value, Is.Not.Null);
            // force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(500);
            GC.Collect(); // Force another collection

            // Assert
            Assert.That(weakRefKey1.Equals(weakRefKey2), Is.False);
        }

        private class Foo
        {
            public string Bar { get; set; }
        }
    }

}
