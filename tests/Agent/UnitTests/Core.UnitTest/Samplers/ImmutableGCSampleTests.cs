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
        public void Constructor_WithParameters_ShouldInitializeFields()
        {
            // Arrange
            var lastSampleTime = DateTime.UtcNow.AddMinutes(-1);
            var currentSampleTime = DateTime.UtcNow;
            var totalMemoryBytes = 1024L;
            var totalAllocatedBytes = 2048L;
            var totalCommittedBytes = 4096L;
            var heapSizesBytes = new long[] { 100, 200, 300, 400, 500 };
            var rawCollectionCounts = new int[] { 4, 3, 2, 1, 0 };
            var fragmentationSizesBytes = new long[] { 10, 20, 30, 40, 50 };

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
                Assert.That(sample.GCCollectionCounts[0], Is.EqualTo(1)); // Gen 1
                Assert.That(sample.GCCollectionCounts[1], Is.EqualTo(1)); // Gen 2
                Assert.That(sample.GCCollectionCounts[2], Is.EqualTo(1)); // Gen 3
                Assert.That(sample.GCCollectionCounts[3], Is.EqualTo(1)); // LOH
                Assert.That(sample.GCCollectionCounts[4], Is.EqualTo(0)); // POH
            });
        }
    }
}
