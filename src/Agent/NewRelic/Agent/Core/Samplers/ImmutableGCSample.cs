// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Samplers
{
    public class ImmutableGCSample
    {
        public readonly DateTime LastSampleTime;
        public readonly DateTime CurrentSampleTime;

        public readonly long TotalMemoryBytes; // In-use memory on the GC heap as of current GC
        public readonly long TotalAllocatedBytes; // total memory allocated on GC heap since process start
        public readonly long TotalCommittedBytes;// committed virtual memory as of current GC

        public readonly long[] GCHeapSizesBytes; // heap sizes as of current GC
        public readonly int[] GCCollectionCounts; // number of collections since last sample
        public readonly long[] GCFragmentationSizesBytes; // heap fragmentation as of current GC

        public ImmutableGCSample()
        {
            LastSampleTime = CurrentSampleTime = DateTime.MinValue;
            GCHeapSizesBytes = new long[5];
            GCCollectionCounts = new int[5];
            GCFragmentationSizesBytes = new long[5];
        }

        public ImmutableGCSample(DateTime lastSampleTime, DateTime currentSampleTime, long totalMemoryBytes, long totalAllocatedBytes, long totalCommittedBytes, long[] heapSizesBytes, int[] rawCollectionCounts, long[] fragmentationSizesBytes)
        {
            LastSampleTime = lastSampleTime;
            CurrentSampleTime = currentSampleTime;

            TotalMemoryBytes = totalMemoryBytes;

            TotalAllocatedBytes = totalAllocatedBytes;
            TotalCommittedBytes = totalCommittedBytes;

            GCHeapSizesBytes = heapSizesBytes;
            GCFragmentationSizesBytes = fragmentationSizesBytes;

            // should always be 5, but handle smaller just in case
            var collectionLength = rawCollectionCounts.Length;
            GCCollectionCounts = new int[5]; // we always report 5 samples

            // Gen 1
            GCCollectionCounts[0] = rawCollectionCounts[0] - rawCollectionCounts[1];
            // Gen 2
            GCCollectionCounts[1] = rawCollectionCounts[1] - rawCollectionCounts[2];
            // Gen 3
            GCCollectionCounts[2] = rawCollectionCounts[2];

            // LOH
            if (collectionLength > 3)
                GCCollectionCounts[3] = rawCollectionCounts[3]; // or does this need to be [3] - [4]??

            // POH
            if (collectionLength > 4)
                GCCollectionCounts[4] = rawCollectionCounts[4]; //??
        }
    }
}
