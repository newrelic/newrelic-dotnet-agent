// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Samplers;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Tests.Samplers
{
    [TestFixture]
    public class ImmutableGCSampleTests
    {
        [Test]
        public void Constructor_Default_ShouldInitializeFields()
        {
            // Act
            var sample = new ImmutableGCSample();

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(sample.LastSampleTime, Is.EqualTo(DateTime.MinValue));
                Assert.That(sample.CurrentSampleTime, Is.EqualTo(DateTime.MinValue));
                Assert.That(sample.GCHeapSizesBytes.Length, Is.EqualTo(5));
                Assert.That(sample.GCCollectionCounts.Length, Is.EqualTo(5));
                Assert.That(sample.GCFragmentationSizesBytes.Length, Is.EqualTo(5));
            });
        }

        [Test]
        [TestCase(3, new long[] { 100, 200, 300 }, new[] { 5, 4, 3 }, new long[] { 10, 20, 30 }, new[] { 1, 1, 3, 0, 0 })]
        [TestCase(4, new long[] { 100, 200, 300, 400 }, new[] { 5, 4, 3, 2 }, new long[] { 10, 20, 30, 40 }, new[] { 1, 1, 1, 2, 0})]
        [TestCase(5, new long[] { 100, 200, 300, 400, 500 }, new[] { 5, 4, 3, 2, 1 }, new long[] { 10, 20, 30, 40, 50 }, new[] { 1, 1, 1, 1, 1 })]
        public void Constructor_WithParameters_ShouldInitializeFields(int collectionLength, long[] heapSizesBytes, int[] rawCollectionCounts, long[] fragmentationSizesBytes, int[] expectedCollectionCounts)
        {
            // Arrange
            var lastSampleTime = DateTime.UtcNow.AddMinutes(-1);
            var currentSampleTime = DateTime.UtcNow;
            var totalMemoryBytes = 1024L;
            var totalAllocatedBytes = 2048L;
            var totalCommittedBytes = 4096L;

            // Act
            var sample = new ImmutableGCSample(lastSampleTime, currentSampleTime, totalMemoryBytes, totalAllocatedBytes, totalCommittedBytes, heapSizesBytes, rawCollectionCounts, fragmentationSizesBytes);

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(sample.LastSampleTime, Is.EqualTo(lastSampleTime));
                Assert.That(sample.CurrentSampleTime, Is.EqualTo(currentSampleTime));
                Assert.That(sample.TotalMemoryBytes, Is.EqualTo(totalMemoryBytes));
                Assert.That(sample.TotalAllocatedBytes, Is.EqualTo(totalAllocatedBytes));
                Assert.That(sample.TotalCommittedBytes, Is.EqualTo(totalCommittedBytes));
                Assert.That(sample.GCHeapSizesBytes, Is.EqualTo(heapSizesBytes));
                Assert.That(sample.GCFragmentationSizesBytes, Is.EqualTo(fragmentationSizesBytes));

                // Verify GCCollectionCounts
                Assert.That(sample.GCCollectionCounts.Length, Is.EqualTo(5));
                Assert.That(sample.GCCollectionCounts[0], Is.EqualTo(expectedCollectionCounts[0])); // Gen 0
                Assert.That(sample.GCCollectionCounts[1], Is.EqualTo(expectedCollectionCounts[1])); // Gen 1
                Assert.That(sample.GCCollectionCounts[2], Is.EqualTo(expectedCollectionCounts[2])); // Gen 2
                Assert.That(sample.GCCollectionCounts[3], Is.EqualTo(expectedCollectionCounts[3])); // LOH
                Assert.That(sample.GCCollectionCounts[4], Is.EqualTo(expectedCollectionCounts[4])); // POH
            });
        }
    }
}
