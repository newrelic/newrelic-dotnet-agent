// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
                return RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
            }
        }

        public static bool IsAlpine
        {
            get
            {
                if (IsLinux)
                {
                    var expectedOSName = File.ReadAllLines("/etc/os-release")
                        .First(line => line.StartsWith("ID=", StringComparison.OrdinalIgnoreCase))
                        .Substring("ID=".Length)
                        .Trim('\"', '\'')
                        .ToLower();
                    return expectedOSName == "alpine";
                }
                return false;
            }
        }

        public static string Arch => RuntimeInformation.OSArchitecture.ToString().ToLower();
        public static string CurrentRuntime => $"{(IsLinux ? "linux" : "win")}-{(IsAlpine ? "musl-" : "")}{Arch}";
        public static string RuntimeHomeDirName => $"newrelichome_{Arch}_coreclr{(IsLinux ? "_linux" : "")}";

        public static T ThrowIfNull<T>(T value, string valueName)
        {
            if (value == null)
                throw new ArgumentNullException(valueName);

            return value;
        }
    }
}
