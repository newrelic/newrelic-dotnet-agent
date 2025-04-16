// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.Utilities
{
    public static class HexStringConverter
    {
        public static byte[] FromHexString(this ReadOnlySpan<char> chars)
        {
            if (chars.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have an even length.", nameof(chars));
            }

            byte[] result = new byte[chars.Length / 2];

            for (int i = 0; i < chars.Length; i += 2)
            {
                result[i / 2] = (byte)((GetHexValue(chars[i]) << 4) + GetHexValue(chars[i + 1]));
            }

            return result;
        }

        private static int GetHexValue(char hex)
        {
            if (hex is >= '0' and <= '9')
            {
                return hex - '0';
            }
            else if (hex is >= 'a' and <= 'f')
            {
                return 10 + (hex - 'a');
            }
            else if (hex is >= 'A' and <= 'F')
            {
                return 10 + (hex - 'A');
            }
            else
            {
                throw new ArgumentException($"Invalid hexadecimal character: {hex}");
            }
        }
    }
}
