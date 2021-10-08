// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public static class Utilities
    {
#if DEBUG
		public static string Configuration = "Debug";
#else
        public static string Configuration = "Release";
#endif

        public static bool IsLinux
        {
            get
            {
#if NETFRAMEWORK
            return false;
#endif
#if NETSTANDARD2_0
                return System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux);
#endif
            }
        }

        public static T ThrowIfNull<T>(T value, string valueName)
        {
            if (value == null)
                throw new ArgumentNullException(valueName);

            return value;
        }
    }
}
