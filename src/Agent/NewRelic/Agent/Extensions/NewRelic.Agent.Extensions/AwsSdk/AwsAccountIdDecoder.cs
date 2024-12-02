// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.AwsSdk
{
    public static class AwsAccountIdDecoder
    {
        // magic number
        private const long Mask = 0x7FFFFFFFFF80;

        public static string GetAccountId(string awsAccessKeyId)
        {
            if (string.IsNullOrEmpty(awsAccessKeyId))
            {
                throw new ArgumentNullException(nameof(awsAccessKeyId), "AWS Access Key ID cannot be null or empty.");
            }

            if (awsAccessKeyId.Length < 14)
            {
                throw new ArgumentOutOfRangeException(nameof(awsAccessKeyId), "AWS Access Key ID must be at least 14 characters long.");
            }

            string accessKeyWithoutPrefix = awsAccessKeyId.Substring(4).ToLowerInvariant();
            long encodedAccount = Base32Decode(accessKeyWithoutPrefix);

            return ((encodedAccount & Mask) >> 7).ToString();
        }

        /// <summary>
        /// Performs a Base-32 decode of the specified input string.
        /// Allowed character range is a-z and 2-7. 'a' being 0 and '7' is 31.
        /// </summary>
        /// <param name="src">The string to be decoded. Must be at least 10 characters.</param>
        /// <returns>A long containing first 6 bytes of the base 32 decoded data.</returns>
        /// <exception cref="ArgumentException">If src has less than 10 characters.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If src contains invalid characters for Base-32</exception>
        private static long Base32Decode(string src)
        {
            if (src.Length < 10)
            {
                throw new ArgumentException("The input string must be at least 10 characters long.", nameof(src));
            }

            long baseValue = 0;
            for (int i = 0; i < 10; i++)
            {
                baseValue <<= 5;
                char c = src[i];
                baseValue += c switch
                {
                    >= 'a' and <= 'z' => c - 'a',
                    >= '2' and <= '7' => c - '2' + 26,
                    _ => throw new ArgumentOutOfRangeException(nameof(src),
                        "The input string must contain only characters in the range a-z and 2-7.")
                };
            }

            return baseValue >> 2;
        }
    }
}
