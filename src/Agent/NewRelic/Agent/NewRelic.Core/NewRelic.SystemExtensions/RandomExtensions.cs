/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
namespace NewRelic.SystemExtensions
{
    public static class RandomExtensions
    {
        /// <summary>Generates a random number between [min, max).</summary>
        /// <param name="random"></param>
        /// <param name="max">Exclusive upper bound.</param>
        /// <returns>A random number in the range [0, max)</returns>
        /// <remarks>Thanks Will/PHP team.</remarks>
        public static ulong Next64(this Random random, ulong max)
        {
            // figure out the largest number we can generate with even distribution between 0 and max
            var largestMultiple = ulong.MaxValue - (ulong.MaxValue % max);
            var bytes = new byte[16];
            ulong random64;
            // generate random 64-bit numbers until we get one that is less than largestMultiple
            do
            {
                // generate a random number between 0 and UInt64.MaxValue
                random.NextBytes(bytes);
                random64 = BitConverter.ToUInt64(bytes, 0);
            }
            while (random64 >= largestMultiple);
            {
                return random64 % max;
            }
        }
    }
}
