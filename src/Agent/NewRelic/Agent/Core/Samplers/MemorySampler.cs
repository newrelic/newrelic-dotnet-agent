// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
    public class MemorySampler : AbstractSampler
    {
        private readonly IMemorySampleTransformer _memorySampleTransformer;

        private readonly IProcessStatic _processStatic;

        private const int MemorySampleIntervalSeconds = 60;

        public MemorySampler(IScheduler scheduler, IMemorySampleTransformer memorySampleTransformer, IProcessStatic processStatic)
            : base(scheduler, TimeSpan.FromSeconds(MemorySampleIntervalSeconds))
        {
            _memorySampleTransformer = memorySampleTransformer;
            _processStatic = processStatic;
        }

        public override void Sample()
        {
            _processStatic.GetCurrentProcess().Refresh();

            try
            {
                var immutableMemorySample = new ImmutableMemorySample(GetCurrentProcessPrivateMemorySize(), GetCurrentProcessWorkingSet());
                _memorySampleTransformer.Transform(immutableMemorySample);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to get Memory sample.  No Memory metrics will be reported.");
                Stop();
            }
        }

        private long GetCurrentProcessPrivateMemorySize()
        {
            return _processStatic.GetCurrentProcess().PrivateMemorySize64;
        }
        private long GetCurrentProcessWorkingSet()
        {
            return _processStatic.GetCurrentProcess().WorkingSet64;
        }
    }

    public class ImmutableMemorySample
    {
        /// <summary>
        /// Process.PrivateMemorySize64; metric name = Memory/Physical
        /// </summary>
        public readonly long MemoryPrivate;

        /// <summary>
        /// Process.WorkingSet64; metric name = Memory/WorkingSet
        /// </summary>
        public readonly long MemoryWorkingSet;

        public ImmutableMemorySample(long memoryPrivate, long memoryWorkingSet)
        {
            MemoryPrivate = memoryPrivate;
            MemoryWorkingSet = memoryWorkingSet;
        }
    }
}
