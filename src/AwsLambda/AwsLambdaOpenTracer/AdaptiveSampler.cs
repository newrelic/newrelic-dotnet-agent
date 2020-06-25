/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using NewRelic.Core;
using System.Threading;
using System.Collections.Generic;

namespace NewRelic.OpenTracing.AmazonLambda
{
    internal class AdaptiveSampler
    {
        private const double BackOffExponent = 0.5;
        private SpinLock _spinLock = new SpinLock();
        private readonly Random _randomNumberGenerator;
        private readonly double SamplingTargetPeriodInMilliseconds;

        //_ceilingValuesForBackoff values are used for the TargetSamplesPerInterval+1, TargetSamplesPerInterval+2,... candidates in an interval.
        private readonly List<int> _ceilingValuesForBackoff;

        private int _sampledTrueCount = 0;
        private int _decidedCount = 0;
        private int _decidedCountLast = 0;

        private long _lastStart = 0;
        private bool _firstPeriod = true;

        public AdaptiveSampler(int target, int samplingTargetPeriodInSeconds, Random randomNumberGenerator)
        {
            _randomNumberGenerator = randomNumberGenerator;
            _ceilingValuesForBackoff = ComputeCeilingValuesForBackOff(target);
            Target = target;
            SamplingTargetPeriodInSeconds = samplingTargetPeriodInSeconds;
            SamplingTargetPeriodInMilliseconds = TimeSpan.FromSeconds(SamplingTargetPeriodInSeconds).TotalMilliseconds;
        }

        internal void Reset()
        {
            _lastStart = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            _firstPeriod = false;
            Interlocked.Exchange(ref _sampledTrueCount, 0);
            Interlocked.Exchange(ref _decidedCountLast, _decidedCount);
            Interlocked.Exchange(ref _decidedCount, 0);
        }

        public bool ComputeSampled()
        {
            var sampled = false;

            if (_firstPeriod)
            {
                sampled = _sampledTrueCount < Target;
            }
            else if (_sampledTrueCount < Target)
            {
                sampled = GetRandomIntForSampled(_decidedCountLast) < Target;
            }
            else
            {
                //sample only if random number is below the ceiling
                var ceilingValue = CeilingFromSamplesInCurrentInterval(_sampledTrueCount);
                if (ceilingValue > 0)
                {
                    sampled = GetRandomIntForSampled(_decidedCount) < ceilingValue;
                }

            }

            Interlocked.Increment(ref _decidedCount);

            if (sampled)
            {
                Interlocked.Increment(ref _sampledTrueCount);
            }

            return sampled;
        }

        public void RequestStarted()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (_lastStart <= 0)
            {
                _lastStart = now;
            }

            if (now >= _lastStart + SamplingTargetPeriodInMilliseconds)
            {
                Reset();
            }
        }

        public int Target { get; internal set; }

        public int SamplingTargetPeriodInSeconds { get; internal set; }

        private int GetRandomIntForSampled(int upperBound)
        {
            int value = upperBound;
            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                value = _randomNumberGenerator.Next(upperBound);
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }

            return value;
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
            var ceilingIndex = candidatesSampledCurrentInterval - Target;
            return ceilingIndex < _ceilingValuesForBackoff.Count ? _ceilingValuesForBackoff[ceilingIndex] : 0;
        }


    }
}
