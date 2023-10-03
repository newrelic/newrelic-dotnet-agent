// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Threading;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Samplers
{
    public class GCSamplerNetCore : AbstractSampler
    {
        public struct SamplerIsApplicableToFrameworkResult
        {
            public bool Result { get; set; }

            public SamplerIsApplicableToFrameworkResult(bool value)
            {
                Result = value;
            }
        }

        private ISampledEventListener<Dictionary<GCSampleType, float>> _listener;
        private readonly Func<ISampledEventListener<Dictionary<GCSampleType, float>>> _eventListenerFactory;
        private readonly IGcSampleTransformer _transformer;
        private const int GCSampleNetCoreIntervalSeconds = 60;

        protected override bool Enabled => base.Enabled
            && (_configuration?.EventListenerSamplersEnabled).GetValueOrDefault(false)
            && _fxSamplerIsApplicableToFramework().Result;

        /// <summary>
        /// Method determines if the event source that we are interested in will be available on this framework (net-core vs. net-framework).
        /// If the value is false, this sampler should be disabled
        /// </summary>
        /// <returns></returns>
        private Func<SamplerIsApplicableToFrameworkResult> _fxSamplerIsApplicableToFramework;

        /// <summary>
        /// Backing property for similarly named function.
        /// </summary>
        private static bool? _fxSamplerIsApplicableToFrameworkDefaultValue = null;

        /// <summary>
        /// This method was taken from .Net Core code.  It validates that the current application is a .Net Core 2.2
        /// or above application.
        /// </summary>
        /// <returns></returns>
        public static SamplerIsApplicableToFrameworkResult FXsamplerIsApplicableToFrameworkDefault()
        {
            if (_fxSamplerIsApplicableToFrameworkDefaultValue.HasValue)
            {
                return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
            }

            if (!AgentInstallConfiguration.IsNetCore30OrAbove)
            {
                _fxSamplerIsApplicableToFrameworkDefaultValue = false;
                return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
            }

            _fxSamplerIsApplicableToFrameworkDefaultValue = true;
            return new SamplerIsApplicableToFrameworkResult(_fxSamplerIsApplicableToFrameworkDefaultValue.Value);
        }

        public GCSamplerNetCore(IScheduler scheduler, Func<ISampledEventListener<Dictionary<GCSampleType, float>>> eventListenerFactory, IGcSampleTransformer transformer, Func<SamplerIsApplicableToFrameworkResult> fxSamplerIsApplicableToFramework) : base(scheduler, TimeSpan.FromSeconds(GCSampleNetCoreIntervalSeconds))
        {
            _eventListenerFactory = eventListenerFactory;
            _transformer = transformer;
            _fxSamplerIsApplicableToFramework = fxSamplerIsApplicableToFramework;
        }

        public override void Sample()
        {
            if (_listener == null)
            {
                return;
            }

            try
            {
                var sampleValues = _listener.Sample();
                _transformer.Transform(sampleValues);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to get Garbage Collection event listener sample.  Further .NetCore GC metrics will not be collected.");
                Dispose();
            }
        }

        public override void Start()
        {
            try
            {
                if (!_fxSamplerIsApplicableToFramework().Result)
                {
                    Log.Debug($"The GCSamplerNetCore sampler has been disabled by configuration or because this is not a .Net Core 3.0+ application.");
                }

                base.Start();

                if (!Enabled)
                {
                    return;
                }

                _listener = _listener ?? _eventListenerFactory();
                _listener.StartListening();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unable to start Garbage Collection Event Listener Sample.  Further .NetCore GC metrics will not be captured.");
                Dispose();
            }
        }

        protected override void Stop()
        {
            base.Stop();
            _listener?.StopListening();
        }

        public override void Dispose()
        {
            base.Dispose();
            _listener?.StopListening();
            _listener?.Dispose();
            _listener = null;
        }
    }

    public class GCEventsListener : SampledEventListener<Dictionary<GCSampleType, float>>
    {
        //The Microsoft Event Source to listen to "Microsoft-Windows-DotNETRuntime"
        public static readonly string DotNetEventSourceName = "Microsoft-Windows-DotNETRuntime";

        /// <summary>
        /// Allows us to mainpulate the event source ID for testing purposes
        /// </summary>
        public static string EventSourceNameToMonitor = DotNetEventSourceName;

        public const int GCKeyword = 0x1;
        public const int EventID_GCStart = 1;
        public const int EventID_GCHeapStats = 4;

        //These values are stored as objects so as to avoid extra castings
        private object _gen0Size = (ulong)0;
        private object _gen0Promoted = (ulong)0;
        private object _gen1Size = (ulong)0;
        private object _gen1Promoted = (ulong)0;
        private object _gen2Size = (ulong)0;
        private object _gen2Promoted = (ulong)0;
        private object _lohSize = (ulong)0;
        private object _lohPromoted = (ulong)0;
        private object _handlesCount = (uint)0;

        private InterlockedCounter _inducedCount = new InterlockedCounter();

        private readonly InterlockedCounter[] _collectionCountPerGen;

        public GCEventsListener()
        {
            _collectionCountPerGen = new InterlockedCounter[GC.MaxGeneration + 1];
            for (var i = 0; i < _collectionCountPerGen.Length; i++)
            {
                _collectionCountPerGen[i] = new InterlockedCounter();
            }
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {

            if (eventSource.Name == EventSourceNameToMonitor)
            {
                _eventSource = eventSource;
                StartListening();
                base.OnEventSourceCreated(eventSource);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            switch (eventData.EventId)
            {
                case EventID_GCStart:
                    ProcessEvent_GCStart(eventData);
                    break;

                case EventID_GCHeapStats:
                    ProcessEvent_GCHeapStats(eventData);
                    break;
            }
        }

        private void ProcessEvent_GCStart(EventWrittenEventArgs eventArgs)
        {
            //Identify if the GC was induced by code
            const uint reasonID_Induced = 0x1;
            const uint reasonID_InducedNotBlocking = 0x7;

            var reason = (uint)eventArgs.Payload[2];
            if (reason == reasonID_Induced || reason == reasonID_InducedNotBlocking)
            {
                _inducedCount?.Increment();
            }

            //Update Collection Counters for each generation including and below the GC depth requested
            var depth = (uint)eventArgs.Payload[1];
            for (var i = 0; i <= depth; i++)
            {
                _collectionCountPerGen?[i]?.Increment();
            }
        }

        private void ProcessEvent_GCHeapStats(EventWrittenEventArgs eventArgs)
        {
            Interlocked.Exchange(ref _gen0Size, eventArgs.Payload[0]);
            Interlocked.Exchange(ref _gen0Promoted, eventArgs.Payload[1]);
            Interlocked.Exchange(ref _gen1Size, eventArgs.Payload[2]);
            Interlocked.Exchange(ref _gen1Promoted, eventArgs.Payload[3]);
            Interlocked.Exchange(ref _gen2Size, eventArgs.Payload[4]);
            Interlocked.Exchange(ref _gen2Promoted, eventArgs.Payload[5]);
            Interlocked.Exchange(ref _lohSize, eventArgs.Payload[6]);
            Interlocked.Exchange(ref _lohPromoted, eventArgs.Payload[7]);
            Interlocked.Exchange(ref _handlesCount, eventArgs.Payload[12]);
        }

        public override Dictionary<GCSampleType, float> Sample()
        {
            var inducedCount = _inducedCount.Exchange(0);

            var result = new Dictionary<GCSampleType, float>()
            {
                { GCSampleType.Gen0Size, (ulong)_gen0Size },
                { GCSampleType.Gen0Promoted, (ulong)_gen0Promoted },
                { GCSampleType.Gen1Size, (ulong)_gen1Size },
                { GCSampleType.Gen1Promoted, (ulong)_gen1Promoted },
                { GCSampleType.Gen2Size, (ulong)_gen2Size },
                { GCSampleType.Gen2Survived, (ulong)_gen2Promoted },
                { GCSampleType.LOHSize, (ulong)_lohSize },
                { GCSampleType.LOHSurvived, (ulong)_lohPromoted },
                { GCSampleType.HandlesCount, (uint)_handlesCount },
                { GCSampleType.InducedCount, inducedCount }
            };

            if (_collectionCountPerGen.Length > 0) result[GCSampleType.Gen0CollectionCount] = _collectionCountPerGen[0].Exchange(0);
            if (_collectionCountPerGen.Length > 1) result[GCSampleType.Gen1CollectionCount] = _collectionCountPerGen[1].Exchange(0);
            if (_collectionCountPerGen.Length > 2) result[GCSampleType.Gen2CollectionCount] = _collectionCountPerGen[2].Exchange(0);

            return result;
        }

        public override void StartListening()
        {
            if (_eventSource != null)
            {
                EnableEvents(_eventSource, EventLevel.Informational, (EventKeywords)GCKeyword);
            }
        }
    }
}
