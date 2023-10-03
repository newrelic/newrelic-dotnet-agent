// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Samplers
{
    public enum GCSampleType
    {
        Gen0Size,
        Gen0Promoted,
        Gen1Size,
        Gen1Promoted,
        Gen2Size,
        Gen2Survived,
        LOHSize,
        LOHSurvived,
        HandlesCount,
        InducedCount,
        PercentTimeInGc,
        Gen0CollectionCount,
        Gen1CollectionCount,
        Gen2CollectionCount
    }

#if NETFRAMEWORK

    public class GcSampler : AbstractSampler
    {
        private const string GCPerfCounterCategoryName = ".NET CLR Memory";
        private const int MaxConsecutiveFailuresBeforeDisable = 5;

        private readonly IGcSampleTransformer _transformer;
        private const int GcSampleIntervalSeconds = 60;
        private readonly IPerformanceCounterProxyFactory _pcProxyFactory;

        private int _countConsecutiveSamplingFailures = 0;
        private string _perfCounterInstanceName;

        private const string LOG_PREFIX = "GC Performance Counters:";

        /// <summary>
        /// Translates the sample type enum to the name of the windows performance counter
        /// </summary>
        private readonly Dictionary<GCSampleType, string> _perfCounterNames = new Dictionary<GCSampleType, string>
        {
            { GCSampleType.Gen0Size , "Gen 0 heap size"},					// BuildByteData (MB)
			{ GCSampleType.Gen0Promoted , "Promoted Memory from Gen 0"},	// BuildByteData
			{ GCSampleType.Gen1Size , "Gen 1 heap size"},					// BuildByteData
			{ GCSampleType.Gen1Promoted , "Promoted Memory from Gen 1"},	// BuildByteData
			{ GCSampleType.Gen2Size , "Gen 2 heap size"},					// BuildByteData
			{ GCSampleType.LOHSize , "Large Object Heap size"},				// BuildByteData
			{ GCSampleType.HandlesCount , "# GC Handles"},					// BuildGaugeValue
			{ GCSampleType.InducedCount , "# Induced GC"},					// BuildGaugeValue ?
			{ GCSampleType.PercentTimeInGc , "% Time in GC"},				// BuildPercentageData
			{ GCSampleType.Gen0CollectionCount , "# Gen 0 Collections"},	// reset on sample}, BuildCountData
			{ GCSampleType.Gen1CollectionCount , "# Gen 1 Collections"},	// reset on sample}, BuildCountData
			{ GCSampleType.Gen2CollectionCount , "# Gen 2 Collections"}		// reset on sample}, BuildCountData
		};

        private readonly Dictionary<GCSampleType, Func<IPerformanceCounterProxy, float>> _perfCounterValueHandlers;

        private float GetPerfCounterValue_Raw(IPerformanceCounterProxy proxy)
        {
            return proxy.NextValue();
        }

        private float GetPerfCounterValue_DeltaValue(IPerformanceCounterProxy proxy, ref float prevValue)
        {
            var currentPerfCounterVal = proxy.NextValue();
            var prevPerfCounterVal = Interlocked.Exchange(ref prevValue, currentPerfCounterVal);
            return currentPerfCounterVal - prevPerfCounterVal;
        }

        private float GetPerfCounterValue_Gen0CollectionCount(IPerformanceCounterProxy proxy)
        {
            return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen0CollectionCount);
        }

        private float GetPerfCounterValue_Gen1CollectionCount(IPerformanceCounterProxy proxy)
        {
            return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen1CollectionCount);
        }

        private float GetPerfCounterValue_Gen2CollectionCount(IPerformanceCounterProxy proxy)
        {
            return GetPerfCounterValue_DeltaValue(proxy, ref _PrevGen2CollectionCount);
        }

        private float GetPerfCounterValue_InducedCount(IPerformanceCounterProxy proxy)
        {
            return GetPerfCounterValue_DeltaValue(proxy, ref _PrevInducedCount);
        }

        /// <summary>
        /// Holds the performance counter proxies used to capture the data from the windows performance counters.
        /// </summary>
        private Dictionary<GCSampleType, IPerformanceCounterProxy> _perfCounterProxies;

        // These hold previous values to compute delta between samplings.
        private float _PrevGen0CollectionCount = 0;
        private float _PrevGen1CollectionCount = 0;
        private float _PrevGen2CollectionCount = 0;
        private float _PrevInducedCount = 0;

        public GcSampler(IScheduler scheduler, IGcSampleTransformer gcSampleTransformer, IPerformanceCounterProxyFactory pcProxyFactory)
         : base(scheduler, TimeSpan.FromSeconds(GcSampleIntervalSeconds))
        {
            _pcProxyFactory = pcProxyFactory;
            _transformer = gcSampleTransformer;

            //Assign functions to handle the values for the different performance counters collected.
            _perfCounterValueHandlers = new Dictionary<GCSampleType, Func<IPerformanceCounterProxy, float>>()
            {
                { GCSampleType.Gen0Size, GetPerfCounterValue_Raw },
                { GCSampleType.Gen0Promoted , GetPerfCounterValue_Raw },
                { GCSampleType.Gen1Size, GetPerfCounterValue_Raw },
                { GCSampleType.Gen1Promoted, GetPerfCounterValue_Raw },
                { GCSampleType.Gen2Size, GetPerfCounterValue_Raw },
                { GCSampleType.LOHSize, GetPerfCounterValue_Raw },
                { GCSampleType.HandlesCount, GetPerfCounterValue_Raw },
                { GCSampleType.InducedCount, GetPerfCounterValue_InducedCount },
                { GCSampleType.PercentTimeInGc, GetPerfCounterValue_Raw },
                { GCSampleType.Gen0CollectionCount, GetPerfCounterValue_Gen0CollectionCount },
                { GCSampleType.Gen1CollectionCount, GetPerfCounterValue_Gen1CollectionCount },
                { GCSampleType.Gen2CollectionCount, GetPerfCounterValue_Gen2CollectionCount }
            };
        }

        public override void Start()
        {
            if (!Enabled)
            {
                Log.Debug($"The GC Sampler is NOT enabled");
                //We always want to do this because we don't want to code with the internals 
                //of the base class in mind.
                base.Start();
                return;
            }

            _perfCounterInstanceName = null;
            _countConsecutiveSamplingFailures = 0;

            //Defer the creation of Performance Counter Proxies until the first sampling occurs.
            //Performance Counter Instance Names can change based on the number of running processes
            //for the same executable.

            base.Start();
        }

        protected override void Stop()
        {
            base.Stop();

            if (_perfCounterProxies == null)
            {
                return;
            }

            var proxies = Interlocked.Exchange(ref _perfCounterProxies, null);

            DisposeProxies(proxies.Values);
        }

        private void DisposeProxies(IEnumerable<IPerformanceCounterProxy> proxies)
        {
            foreach (var perfCounterProxy in proxies)
            {
                perfCounterProxy.Dispose();
            }
        }

        private int CreatePerfCounterProxies(string processInstanceName)
        {
            var perfCounterProxies = new Dictionary<GCSampleType, IPerformanceCounterProxy>();

            var failedSampleTypes = new List<GCSampleType>();

            foreach (var sampleTypeEnum in _perfCounterNames.Keys)
            {
                try
                {
                    perfCounterProxies[sampleTypeEnum] = _pcProxyFactory.CreatePerformanceCounterProxy(GCPerfCounterCategoryName, _perfCounterNames[sampleTypeEnum], processInstanceName);
                }
                catch (UnauthorizedAccessException)
                {
                    throw;  //Let the caller receive this so that they may shutdown the sampler.
                }
                catch (Exception ex)
                {
                    failedSampleTypes.Add(sampleTypeEnum);
                    Log.Debug(ex, "{prefix} Error encountered during the creation of performance counter for '{sampleTypeEnum}' for Performance Counter Instance '{processInstanceName}'.  Metrics of this type will not be captured.", LOG_PREFIX, sampleTypeEnum, processInstanceName);
                }
            }

            //Print message indicating which perf counter proxies failed to start.
            if (failedSampleTypes.Count > 0)
            {
                var msgToken = string.Join(", ", failedSampleTypes.Select(x => x.ToString()).ToArray());
                Log.Warn("{prefix} The following Performance Counters for Performance Counter Instance '{processInstanceName}' could not be started: {msgToken}.  Debug Level logs will contain more information.", LOG_PREFIX, processInstanceName, msgToken);
            }

            var oldProxies = Interlocked.Exchange(ref _perfCounterProxies, perfCounterProxies);

            if (_perfCounterProxies.Count > 0)
            {
                Log.Debug("{prefix} Sampler is collecting {count} performance counter(s) for Performance Counter Instance '{processInstanceName}'.", LOG_PREFIX, _perfCounterProxies.Count, processInstanceName);
            }

            if (oldProxies != null && oldProxies.Count > 0)
            {
                DisposeProxies(oldProxies.Values);
            }

            return _perfCounterProxies.Count;
        }

        /// <summary>
        /// Method ensures that the performance counters are available and up-to-date.
        /// If the Performance Counter Instance name has changed since last sampling, create new performance
        /// counters.
        /// </summary>
        /// <returns>bool indicating success of doing so</returns>
        private bool EnsurePerformanceCounters(string currentProcessInstanceName)
        {
            if (currentProcessInstanceName == _perfCounterInstanceName && _perfCounterProxies != null && _perfCounterProxies.Count > 0)
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(_perfCounterInstanceName))
            {
                Log.Debug("{prefix} Performance Counter Instance name change detected, rebuilding performance counters:  changed from '{_perfCounterInstanceName}' to '{currentProcessInstanceName}'", LOG_PREFIX, _perfCounterInstanceName, currentProcessInstanceName);
            }

            _perfCounterInstanceName = currentProcessInstanceName;

            var countPerfCountersCreated = CreatePerfCounterProxies(_perfCounterInstanceName);

            return countPerfCountersCreated > 0;
        }

        private void HandleUnauthorizedException()
        {
            var userName = "<unknown>";
            try
            {
                userName = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            }
            catch
            { }

            Log.Warn("{prefix} The executing user, {userName}, has insufficient permissions to collect Windows Performance Counters.", LOG_PREFIX, userName);

            Stop();
        }

        private void HandleProblem()
        {
            _countConsecutiveSamplingFailures++;

            if (_countConsecutiveSamplingFailures >= MaxConsecutiveFailuresBeforeDisable)
            {
                Log.Warn("{prefix} After {MaxConsecutiveFailuresBeforeDisable} failed attempts, GC Metrics sampling will be disabled.", LOG_PREFIX, MaxConsecutiveFailuresBeforeDisable);
                Stop();
            }
        }

        public override void Sample()
        {
            var currentPerfCounterInstName = default(string);
            try
            {
                currentPerfCounterInstName = _pcProxyFactory.GetCurrentProcessInstanceNameForCategory(GCPerfCounterCategoryName, _perfCounterInstanceName);

                if (string.IsNullOrWhiteSpace(currentPerfCounterInstName))
                {
                    // If there was a prior value from last sampling and now there isn't a value, something is wrong so safely stop the sampler.
                    if (!string.IsNullOrWhiteSpace(_perfCounterInstanceName))
                    {
                        Log.Warn("{prefix} Unable to obtain the current Performance Counter Instance Name. The prior name was '{_perfCounterInstanceName}'.  GC Samples will no longer be collected.", LOG_PREFIX, _perfCounterInstanceName);
                        Stop();
                        return;
                    }

                    Log.Finest("{prefix} Unable to obtain the current Perforance Counter Instance Name. GC Samples will not be collected at this time.  Will try again during next sampling cycle.", LOG_PREFIX);
                    return;
                }
            }
            catch (UnauthorizedAccessException)
            {
                HandleUnauthorizedException();
                return;
            }
            catch (Exception ex)
            {
                Log.Finest(ex, "{prefix} An exception occurred while attempting the to get the current Performance Counter Instance Name", LOG_PREFIX);
                HandleProblem();
                return;
            }

            try
            {
                if (!EnsurePerformanceCounters(currentPerfCounterInstName))
                {
                    var logLevel = _countConsecutiveSamplingFailures + 1 >= MaxConsecutiveFailuresBeforeDisable
                        ? LogLevel.Warn
                        : LogLevel.Debug;

                    Log.LogMessage(logLevel, "{prefix} Unable to instantiate any perf counters.  (Performance Counter Instance '{_perfCounterInstanceName}', attempt #{_countConsecutiveSamplingFailures})", LOG_PREFIX, _perfCounterInstanceName, _countConsecutiveSamplingFailures);

                    HandleProblem();

                    return;
                }

                var sampleValues = new Dictionary<GCSampleType, float>();

                foreach (var proxy in _perfCounterProxies)
                {
                    var val = _perfCounterValueHandlers[proxy.Key](proxy.Value);
                    sampleValues[proxy.Key] = val;
                }

                _transformer.Transform(sampleValues);

                _countConsecutiveSamplingFailures = 0;
            }
            catch (UnauthorizedAccessException)
            {
                HandleUnauthorizedException();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "{prefix} Unable to get GC performance counter sample for Performance Counter Instance '{_perfCounterInstanceName}'.", LOG_PREFIX, _perfCounterInstanceName);

                HandleProblem();
            }
        }
    }
#endif
}
