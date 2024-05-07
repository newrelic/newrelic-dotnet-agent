// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.DistributedTracing;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public interface IAdaptiveSampler
    {
        bool ComputeSampled(ref float priority);
        void StartTransaction();
    }


    public class AdaptiveSampler : ConfigurationBasedService, IAdaptiveSampler
    {
        internal class AdaptiveSamplerState
        {
            private const int DoneWithFirstIntervalSentinel = -1;
            private const double BackOffExponent = 0.5d;

            private readonly object _intervalLockObject = new object();
            private readonly object _sync = new object();
            private readonly Random _rand;

            //_ceilingValuesForBackoff values are used for the TargetSamplesPerInterval+1, TargetSamplesPerInterval+2,... candidates in an interval.
            private readonly List<int> _ceilingValuesForBackoff;

            public int TargetSamplesPerInterval { get; }

            public TimeSpan TargetSamplesInterval { get; }

            //down counter for the first interval...
            //initialized to TargetSamplesPerInterval
            // EndOfSamplingInterval() will set to DoneWithFirstIntervalSentinel at the end of the first interval
            private int _firstIntervalSamples; // 10, 9,...0 (still in first interval), -1 (after first interval)
            private int _candidatesSeenCurrentInterval;
            private int _candidatesSeenLastInterval;
            private int _candidatesSampledCurrentInterval;
            private bool _manualIntervalCheck;

            private DateTimeOffset? _nextIntervalStartTime;

            public AdaptiveSamplerState(int targetSamplesPerInterval, TimeSpan targetSamplesInterval, Random rand, bool manualIntervalCheck)
            {
                _rand = rand;
                _firstIntervalSamples = targetSamplesPerInterval;
                TargetSamplesPerInterval = targetSamplesPerInterval;
                TargetSamplesInterval = targetSamplesInterval;
                _ceilingValuesForBackoff = ComputeCeilingValuesForBackOff(targetSamplesPerInterval);
                _manualIntervalCheck = manualIntervalCheck;
            }

            public AdaptiveSamplerState(int targetSamplesPerInterval, TimeSpan targetSamplesInterval, AdaptiveSamplerState state) : this(targetSamplesPerInterval, targetSamplesInterval, state._rand, state._manualIntervalCheck)
            {
                _candidatesSeenCurrentInterval = state._candidatesSeenCurrentInterval;
                _candidatesSeenLastInterval = state._candidatesSeenLastInterval;
                _candidatesSampledCurrentInterval = state._candidatesSampledCurrentInterval;
                _nextIntervalStartTime = DateTimeOffset.UtcNow + TargetSamplesInterval;
            }

            private int RandomNext(int max)
            {
                lock (_sync) return _rand.Next(max);
            }

            private static List<int> ComputeCeilingValuesForBackOff(int samplingTarget)
            {
                var ceilingValues = new List<int>(samplingTarget);
                for (var candidateOrdinal = samplingTarget; ; ++candidateOrdinal)
                {
                    var ratio = (float)samplingTarget / candidateOrdinal;
                    var ceilingValue = (int)Math.Round(Math.Pow(samplingTarget, ratio) - Math.Pow(samplingTarget, BackOffExponent));
                    if (ceilingValue <= 0)
                    {
                        break;
                    }

                    ceilingValues.Add(ceilingValue);
                }
                return ceilingValues;
            }

            private int CeilingFromSamplesInCurrentInterval(int candidatesSampledCurrentInterval)
            {
                var ceilingIndex = candidatesSampledCurrentInterval - TargetSamplesPerInterval;
                return ceilingIndex < _ceilingValuesForBackoff.Count ? _ceilingValuesForBackoff[ceilingIndex] : 0;
            }

            private void EndOfSamplingInterval(DateTimeOffset newNextIntervalStartTime)
            {
                _nextIntervalStartTime = newNextIntervalStartTime;
                _candidatesSeenLastInterval = _candidatesSeenCurrentInterval;
                _candidatesSeenCurrentInterval = 0;
                _candidatesSampledCurrentInterval = 0;
                _firstIntervalSamples = DoneWithFirstIntervalSentinel;
            }

            /// <summary>
            /// Atomically read _firstIntervalSamples and if it contains a positive value, decrement it and return the original value.
            /// </summary>
            /// <returns>The value of _firstIntervalSamples (prior to any decrement operation)</returns>
            public int GetThenDecrementFirstIntervalSampleCount()
            {
                //handle first interval (_firstIntervalSamples is set to TargetSamplesPerInterval during ctor and DoneWithFirstIntervalSentinel in EndOfSamplingInterval)
                var spinner = new SpinWait();
                int firstIntervalSamples;
                //if firstIntervalSamples is 0 or DoneWithFirstIntervalSentinel, nothing more to do.
                while ((firstIntervalSamples = Volatile.Read(ref _firstIntervalSamples)) > 0)
                {
                    //decrement in a thread-safe way and try again if another thread beat us to it.
                    if (firstIntervalSamples == Interlocked.CompareExchange(ref _firstIntervalSamples, firstIntervalSamples - 1, firstIntervalSamples))
                    {
                        break;
                    }
                    spinner.SpinOnce();
                }

                return firstIntervalSamples;
            }

            public void ManualCheckAndUpdateInterval()
            {
                if (_manualIntervalCheck)
                {
                    CheckAndUpdateIntervalIfNecessary();
                }
            }

            public bool ShouldSample()
            {
                if (!_manualIntervalCheck)
                {
                    CheckAndUpdateIntervalIfNecessary();
                }

                //account for seeing this candidate.  we will subtract one from this count for the duration of the method to get the correct count prior to 
                // accounting for this candidate
                Interlocked.Increment(ref _candidatesSeenCurrentInterval);

                var sampled = false;
                var firstIntervalSamplesOrSentinel = GetThenDecrementFirstIntervalSampleCount();
                switch (firstIntervalSamplesOrSentinel)
                {
                    case DoneWithFirstIntervalSentinel:
                        var spinner = new SpinWait();
                        while (true)
                        {
                            var candidatesSampledCurrentInterval = _candidatesSampledCurrentInterval;
                            var candidatesSeenLastInterval = _candidatesSeenLastInterval;
                            var candidatesSeenCurrentInterval = _candidatesSeenCurrentInterval - 1;
                            Thread.MemoryBarrier();
                            if (candidatesSampledCurrentInterval < TargetSamplesPerInterval)
                            {
                                sampled = RandomNext(candidatesSeenLastInterval) < TargetSamplesPerInterval;
                            }
                            else
                            {
                                //sample only if random number is below the ceiling
                                var ceilingValue = CeilingFromSamplesInCurrentInterval(candidatesSampledCurrentInterval);
                                if (ceilingValue > 0)
                                {
                                    sampled = RandomNext(candidatesSeenCurrentInterval) < ceilingValue;
                                }
                            }

                            if (!sampled)
                            {
                                break;
                            }

                            //so we came to the conclusion to sample this candidate... attempt to bump the count and, if successful, boost the priority and return true.
                            //if another thread came to the the conclusion to sample their candidate and beat us to updating the count, we need to circle back and 
                            //reassess our decision to sample.  If we don't reassess, multiple threads could over-sample.
                            if (candidatesSampledCurrentInterval == Interlocked.CompareExchange(ref _candidatesSampledCurrentInterval,
                                    candidatesSampledCurrentInterval + 1, candidatesSampledCurrentInterval))
                            {
                                break;
                            }

                            spinner.SpinOnce();
                        }
                        break;
                    case 0:  //first interval, no samples left
                        return false;
                    default: //first interval and we have not sampled the target number of samples.
                        return true;
                }
                return sampled;
            }

            private void CheckAndUpdateIntervalIfNecessary()
            {
                var now = DateTimeOffset.UtcNow;

                if (!_nextIntervalStartTime.HasValue)
                {
                    //Not locking here because it's not a big deal if this happens more than once.
                    _nextIntervalStartTime = now + TargetSamplesInterval;
                }

                if (now >= _nextIntervalStartTime)
                {
                    lock (_intervalLockObject)
                    {
                        if (now >= _nextIntervalStartTime)
                        {
                            EndOfSamplingInterval(now + TargetSamplesInterval);
                        }
                    }
                }
            }
        }

        public int TargetSamplesPerInterval => _state.TargetSamplesPerInterval;

        private const int MinTargetSamplesPerInterval = 1;
        private const float PriorityBoost = 1.0f;
        public const int DefaultTargetSamplesPerInterval = 10;
        public const int DefaultTargetSamplingIntervalInSeconds = 60;

        private AdaptiveSamplerState _state;

        public AdaptiveSampler(int targetSamplesPerInterval = DefaultTargetSamplesPerInterval, int targetSamplingIntervalInSeconds = DefaultTargetSamplingIntervalInSeconds, int? seed = null, bool? serverlessMode = null)
        {
            if (targetSamplesPerInterval < MinTargetSamplesPerInterval)
            {
                throw new ArgumentException(
                    $"invalid value provided for parameter; it must be at least {MinTargetSamplesPerInterval}",
                    nameof(targetSamplesPerInterval));
            }

            var rand = (seed.HasValue) ? new Random(seed.Value) : new Random();
            var manualIntervalCheck = serverlessMode.HasValue ? serverlessMode.Value : _configuration.ServerlessModeEnabled;

            //This .ctor does not trigger the start of the sampling interval timer
            _state = new AdaptiveSamplerState(targetSamplesPerInterval, TimeSpan.FromSeconds(targetSamplingIntervalInSeconds), rand, manualIntervalCheck);
        }

        public void StartTransaction()
        {
            _state.ManualCheckAndUpdateInterval();
        }

        /// <summary>
        /// Atomically boost the priority
        /// </summary>
        /// <param name="priority">The priority value to boost</param>
        /// <returns></returns>
        private static void BoostPriority(ref float priority)
        {
            var spinWait = new SpinWait();
            while (true)
            {
                var currentValue = priority;
                //This comparison is safe, because we are not comparing the bits of priority against itself not the boosted value
                if (currentValue == Interlocked.CompareExchange(ref priority, TracePriorityManager.Adjust(currentValue, PriorityBoost), currentValue))
                {
                    break;
                }

                spinWait.SpinOnce();
            }
        }

        public bool ComputeSampled(ref float priority)
        {
            var sampled = _state.ShouldSample();
            if (sampled)
            {
                BoostPriority(ref priority);
            }
            return sampled;
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            if (configurationUpdateSource != ConfigurationUpdateSource.Server
                || !_configuration.DistributedTracingEnabled
                || !_configuration.SamplingTarget.HasValue)
            {
                return;
            }

            var samplingTarget = _configuration.SamplingTarget.Value;

            if (samplingTarget < MinTargetSamplesPerInterval)
            {
                Log.Error(
                    $"invalid value specified in new server configuration; sampling_target must be at least {MinTargetSamplesPerInterval} but was {samplingTarget}. (defaulting to {DefaultTargetSamplesPerInterval})");
                samplingTarget = DefaultTargetSamplesPerInterval;
            }

            var samplingInterval = TimeSpan.FromSeconds(_configuration.SamplingTargetPeriodInSeconds ?? DefaultTargetSamplingIntervalInSeconds);

            //This .ctor will force the start of the sampling interval timer if it wasn't started previously (and we aren't in serverless mode)
            _state = new AdaptiveSamplerState(samplingTarget, samplingInterval, _state);
        }
    }
}
