// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Samplers
{
    public class GCSamplerV2 : AbstractSampler
    {
        private readonly IGCSampleTransformerV2 _transformer;
        private DateTime _lastSampleTime;

        private IGCSamplerV2ReflectionHelper _gCSamplerV2ReflectionHelper;
        private bool _hasGCOccurred;

        private const int GCSamplerV2IntervalSeconds = 60;

        public GCSamplerV2(IScheduler scheduler, IGCSampleTransformerV2 transformer, IGCSamplerV2ReflectionHelper gCSamplerV2ReflectionHelper)
            : base(scheduler, TimeSpan.FromSeconds(GCSamplerV2IntervalSeconds))
        {
            _transformer = transformer;
            _gCSamplerV2ReflectionHelper = gCSamplerV2ReflectionHelper;
            _lastSampleTime = DateTime.UtcNow;
        }

        public override void Sample()
        {
            if (_gCSamplerV2ReflectionHelper.ReflectionFailed)
            {
                Stop();
                Log.Error($"Unable to get GC sample due to reflection error. No GC metrics will be reported.");
                return;
            }

            _hasGCOccurred |= _gCSamplerV2ReflectionHelper.HasGCOccurred;

            if (!_hasGCOccurred) // don't do anything until at least one GC has completed
                return;

            dynamic gcMemoryInfo = _gCSamplerV2ReflectionHelper.GCGetMemoryInfo_Invoker(0); // GCKind.Any
            dynamic generationInfo = _gCSamplerV2ReflectionHelper.GetGenerationInfo(gcMemoryInfo);

            var genInfoLength = generationInfo.Length;
            var heapSizesBytes = new long[genInfoLength];
            var fragmentationSizesBytes = new long[genInfoLength];
            var collectionCounts = new int[genInfoLength];

            var index = 0;
            foreach (var generation in generationInfo)
            {
                var generationIndex = index++;
                heapSizesBytes[generationIndex] = generation.SizeAfterBytes;
                fragmentationSizesBytes[generationIndex] = generation.FragmentationAfterBytes;

                collectionCounts[generationIndex] = GC.CollectionCount(generationIndex);
            }

            var totalMemoryBytes = GC.GetTotalMemory(false);
            var totalAllocatedBytes = (long)_gCSamplerV2ReflectionHelper.GCGetTotalAllocatedBytes_Invoker(false);
            var totalCommittedBytes = gcMemoryInfo.TotalCommittedBytes;

            var currentSampleTime = DateTime.UtcNow;

            var sample = new ImmutableGCSample(currentSampleTime, _lastSampleTime, totalMemoryBytes, totalAllocatedBytes, totalCommittedBytes, heapSizesBytes, collectionCounts, fragmentationSizesBytes);
            _transformer.Transform(sample);
            _lastSampleTime = currentSampleTime;
        }
    }
}
#endif
