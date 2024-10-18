// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core.Samplers
{
    public class GCSamplerModern : AbstractSampler
    {
        private readonly IGCSampleTransformerModern _transformer;
        private DateTime _lastSampleTime;
        private bool _hasGCOccurred;

        private IGCSamplerModernReflectionHelper _gCSamplerModernReflectionHelper;

        private const int GCSamplerModernIntervalSeconds = 60;

        public GCSamplerModern(IScheduler scheduler, IGCSampleTransformerModern transformer, IGCSamplerModernReflectionHelper gCSamplerModernReflectionHelper)
            : base(scheduler, TimeSpan.FromSeconds(GCSamplerModernIntervalSeconds))
        {
            _transformer = transformer;
            _gCSamplerModernReflectionHelper = gCSamplerModernReflectionHelper;
            _lastSampleTime = DateTime.UtcNow;
            _hasGCOccurred = false;
        }

        public override void Sample()
        {
            if (_gCSamplerModernReflectionHelper.ReflectionFailed)
            {
                Stop();
                Log.Error($"Unable to get GC sample due to reflection error. No GC metrics will be reported.");
            }

            _hasGCOccurred |= _gCSamplerModernReflectionHelper.HasGCOccurred;

            if (_hasGCOccurred) // don't do anything until at least one GC has completed
            {

                dynamic gcMemoryInfo = _gCSamplerModernReflectionHelper.GCGetMemoryInfo_Invoker(0); // GCKind.Any
                dynamic generationInfo = _gCSamplerModernReflectionHelper.GetGenerationInfo(gcMemoryInfo);

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
                var totalAllocatedBytes = (long)_gCSamplerModernReflectionHelper.GCGetTotalAllocatedBytes_Invoker(false);
                var totalCommittedBytes = gcMemoryInfo.TotalCommittedBytes;

                var currentSampleTime = DateTime.UtcNow;

                var sample = new ImmutableGCSample(currentSampleTime, _lastSampleTime, totalMemoryBytes, totalAllocatedBytes, totalCommittedBytes, heapSizesBytes, collectionCounts, fragmentationSizesBytes);
                _transformer.Transform(sample);
                _lastSampleTime = currentSampleTime;
            }
        }
    }
}
