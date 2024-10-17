// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq.Expressions;
using System.Reflection;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Reflection;

namespace NewRelic.Agent.Core.Samplers
{
    public class GCSamplerModern : AbstractSampler
    {
        private readonly IGCSampleTransformerModern _transformer;
        private DateTime _lastSampleTime;
        private bool _hasGCOccurred;

        private static Func<object, object> _getGenerationInfo;
        private static bool _reflectionFailed;

        private static Func<object, object> GCGetMemoryInfo_Invoker;
        private static Func<object, object> GCGetTotalAllocatedBytes_Invoker;

        private const int GCSamplerModernIntervalSeconds = 60;

        static GCSamplerModern()
        {
            if (!VisibilityBypasser.Instance.TryGenerateOneParameterStaticMethodCaller("System.Runtime", "System.GC", "GetGCMemoryInfo", "System.GCKind", "System.GCMemoryInfo", out GCGetMemoryInfo_Invoker))
            {
                _reflectionFailed = true;
            }

            if (!_reflectionFailed)
            {
                if (!VisibilityBypasser.Instance.TryGenerateOneParameterStaticMethodCaller("System.Runtime", "System.GC", "GetTotalAllocatedBytes", "System.Boolean", "System.Int64", out GCGetTotalAllocatedBytes_Invoker))
                {
                    _reflectionFailed = true;
                }
            }

            if (!_reflectionFailed)
                _getGenerationInfo = GCMemoryInfoHelper.GenerateGetMemoryInfoMethod();
        }

        public GCSamplerModern(IScheduler scheduler, IGCSampleTransformerModern transformer)
            : base(scheduler, TimeSpan.FromSeconds(GCSamplerModernIntervalSeconds))
        {
            _transformer = transformer;
            _lastSampleTime = DateTime.UtcNow;
            _hasGCOccurred = false;
        }

        public override void Sample()
        {
            if (_reflectionFailed)
            {
                Stop();
                Log.Error($"Unable to get GC sample due to reflection error. No GC metrics will be reported.");
            }

            _hasGCOccurred |= GC.CollectionCount(0) > 0;

            if (_hasGCOccurred) // don't do anything until at least one GC has completed
            {

                dynamic gcMemoryInfo = GCGetMemoryInfo_Invoker(0); // GCKind.Any
                dynamic generationInfo = _getGenerationInfo(gcMemoryInfo);

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
                var totalAllocatedBytes = (long)GCGetTotalAllocatedBytes_Invoker(false);
                var totalCommittedBytes = gcMemoryInfo.TotalCommittedBytes;

                var currentSampleTime = DateTime.UtcNow;

                var sample = new ImmutableGCSample(currentSampleTime, _lastSampleTime, totalMemoryBytes, totalAllocatedBytes, totalCommittedBytes, heapSizesBytes, collectionCounts, fragmentationSizesBytes);
                _transformer.Transform(sample);
                _lastSampleTime = currentSampleTime;
            }
        }
    }

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

            // TODO: verify length is 5 as expected
            GCCollectionCounts = new int[rawCollectionCounts.Length];
            // Gen 1
            GCCollectionCounts[0] = rawCollectionCounts[0] - rawCollectionCounts[1];
            // Gen 2
            GCCollectionCounts[1] = rawCollectionCounts[1] - rawCollectionCounts[2];
            // Gen 3
            GCCollectionCounts[2] = rawCollectionCounts[2];

            // LOH
            GCCollectionCounts[3] = rawCollectionCounts[3]; // or does this need to be [3] - [4]??
            // POH
            GCCollectionCounts[4] = rawCollectionCounts[4]; //??
        }
    }

    internal static class GCMemoryInfoHelper
    {
        /// <summary>
        /// Generate a function that takes a GCMemoryInfo instance as an input parameter and 
        /// returns an array of GCGenerationInfo instances.
        /// </summary>
        public static Func<object, object> GenerateGetMemoryInfoMethod()
        {
            var assembly = Assembly.Load("System.Runtime");
            var gcMemoryInfoType = assembly.GetType("System.GCMemoryInfo");

            // Define a parameter expression for the input object
            var inputParameter = Expression.Parameter(typeof(object), "input");

            // Cast the input parameter to GCMemoryInfo
            var gcMemoryInfoParameter = Expression.Convert(inputParameter, gcMemoryInfoType);

            // Get the GenerationInfo property
            var generationInfoProperty = gcMemoryInfoType.GetProperty("GenerationInfo");

            // Access the GenerationInfo property
            var accessGenerationInfo = Expression.Property(gcMemoryInfoParameter, generationInfoProperty);

            // Get the ReadOnlySpan<GCGenerationInfo> type using the full type name
            var readOnlySpanType = assembly.GetType("System.ReadOnlySpan`1[[System.GCGenerationInfo, System.Private.CoreLib]]");

            // Get the ToArray method of ReadOnlySpan<GCGenerationInfo>
            var toArrayMethod = readOnlySpanType.GetMethod("ToArray", BindingFlags.Public | BindingFlags.Instance);

            // Call ToArray() on GenerationInfo
            var callToArray = Expression.Call(accessGenerationInfo, toArrayMethod);

            // Create a lambda expression
            var lambda = Expression.Lambda<Func<object, object>>(Expression.Convert(callToArray, typeof(object)), inputParameter);

            // Compile the lambda expression into a delegate
            return lambda.Compile();
        }

    }
}
