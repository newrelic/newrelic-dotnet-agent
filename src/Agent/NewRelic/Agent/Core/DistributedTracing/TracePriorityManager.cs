// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Agent.Core.DistributedTracing
{
    public interface ITracePriorityManager
    {
        float Create();
    }

    public class TracePriorityManager : ITracePriorityManager
    {
        private SpinLock _spinLock = new SpinLock();
        private readonly Random _randomNumberGenerator;
        private const uint SanitizeShiftDecimalPoint = 1000000;

        public TracePriorityManager(int? seed = null)
        {
            _randomNumberGenerator = seed.HasValue ? new Random(seed.Value) : new Random();
        }

        private static float Sanitize(float priority)
        {
            //truncates to six digits to the right of the decimal point
            return (float)(uint)(priority * SanitizeShiftDecimalPoint) / SanitizeShiftDecimalPoint;
        }

        /// <summary>
        /// Creates the a new random, sanitized priority between 0.0 and 1.0. (A sanitized priority is truncated to to six digits to the right of the decimal point.)
        /// </summary>
        /// <param name="priority">A priority value to adjust</param>
        /// <param name="adjustment">The amount to adjust the priority.  Can be a negative or positive value</param>
        /// <returns>A sanitized priority value (truncated to six digits to the right of the decimal point.</returns>
        public float Create()
        {
            float value;
            var lockTaken = false;
            try
            {
                _spinLock.Enter(ref lockTaken);
                value = (float)_randomNumberGenerator.NextDouble();
            }
            finally
            {
                if (lockTaken)
                {
                    _spinLock.Exit();
                }
            }
            return Sanitize(value);
        }

        /// <summary>
        /// Adjust the given <paramref name="priority"/> by adding the <paramref name="adjustment"/> value and returning a sanitized priority. 
        /// </summary>
        /// <param name="priority">A priority value to adjust</param>
        /// <param name="adjustment">The amount to adjust the priority.  Can be a negative or positive value</param>
        /// <returns>A sanitized priority value (A sanitized priority is truncated to to six digits to the right of the decimal point.)</returns>
        public static float Adjust(float priority, float adjustment)
        {
            return Sanitize(priority + adjustment);
        }
    }
}
