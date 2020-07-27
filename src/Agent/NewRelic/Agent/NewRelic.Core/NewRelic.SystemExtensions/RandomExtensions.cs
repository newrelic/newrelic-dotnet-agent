using System;
using JetBrains.Annotations;
namespace NewRelic.SystemExtensions
{
    public static class RandomExtensions
    {
        /// <summary>Generates a random number between [min, max).</summary>
        /// <param name="random"></param>
        /// <param name="max">Exclusive upper bound.</param>
        /// <returns>A random number in the range [0, max)</returns>
        /// <remarks>Thanks Will/PHP team.</remarks>
        public static UInt64 Next64([NotNull] this Random random, UInt64 max)
        {
            // figure out the largest number we can generate with even distribution between 0 and max
            var largestMultiple = UInt64.MaxValue - (UInt64.MaxValue % max);
            var bytes = new Byte[16];
            UInt64 random64;
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
