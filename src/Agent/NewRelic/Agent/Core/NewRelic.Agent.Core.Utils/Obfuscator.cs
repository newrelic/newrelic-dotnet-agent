// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Text;

namespace NewRelic.Agent.Core.NewRelic.Agent.Core.Utils
{
    public static class Obfuscator
    {
        public static string ObfuscateNameUsingKey(string name, string key)
        {
            var encodedBytes = Encoding.UTF8.GetBytes(name);
            var keyBytes = Encoding.UTF8.GetBytes(key);
            return Convert.ToBase64String(Encode(encodedBytes, keyBytes));
        }
        public static string DeobfuscateNameUsingKey(string name, string key)
        {
            var bytes = Convert.FromBase64String(name);
            var keyBytes = Encoding.UTF8.GetBytes(key);

            var buffer = Encode(bytes, keyBytes);
            return Encoding.UTF8.GetString(buffer, 0, buffer.Length);
        }

        private static byte[] Encode(byte[] bytes, byte[] keyBytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ keyBytes[i % keyBytes.Length]);
            }
            return bytes;
        }
    }
}
