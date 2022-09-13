// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETFRAMEWORK
using Microsoft.Win32;
#endif

namespace NewRelic.Core
{
    public enum DotnetFrameworkVersion
    {
        LessThan45,
        net45,
        net451,
        net452,
        net46,
        net461,
        net462,
        net47,
        net471,
        net472,
        net48
    }

    public enum DotnetCoreVersion
    {
        LessThan30,
        netcoreapp30,
        netcoreapp31,
        net5,
        net6,
        net7,
        Other
    }

    public static class DotnetVersion
    {
#if NETFRAMEWORK
        // This code is derived from: https://docs.microsoft.com/en-us/dotnet/framework/migration-guide/how-to-determine-which-versions-are-installed#net_d
        public static DotnetFrameworkVersion GetDotnetFrameworkVersion()
		{
			const string subkey = @"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full\";

			using (var ndpKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32).OpenSubKey(subkey))
			{
				if (ndpKey != null && ndpKey.GetValue("Release") != null)
				{
					return CheckFor45PlusVersion((int)ndpKey.GetValue("Release"));
				}
				else
				{
					return DotnetFrameworkVersion.LessThan45;
				}
			}

			// Checking the version using >= enables forward compatibility.
			DotnetFrameworkVersion CheckFor45PlusVersion(int releaseKey)
			{
				if (releaseKey >= 528040)
					return DotnetFrameworkVersion.net48;
				if (releaseKey >= 461808)
					return DotnetFrameworkVersion.net472;
				if (releaseKey >= 461308)
					return DotnetFrameworkVersion.net471;
				if (releaseKey >= 460798)
					return DotnetFrameworkVersion.net47;
				if (releaseKey >= 394802)
					return DotnetFrameworkVersion.net462;
				if (releaseKey >= 394254)
					return DotnetFrameworkVersion.net461;
				if (releaseKey >= 393295)
					return DotnetFrameworkVersion.net46;
				if (releaseKey >= 379893)
					return DotnetFrameworkVersion.net452;
				if (releaseKey >= 378675)
					return DotnetFrameworkVersion.net451;
				if (releaseKey >= 378389)
					return DotnetFrameworkVersion.net45;
				// This code should never execute. A non-null release key should mean
				// that 4.5 or later is installed.
				return DotnetFrameworkVersion.LessThan45;
			}
		}
#else
        public static DotnetCoreVersion GetDotnetCoreVersion()
        {
            var envVer = System.Environment.Version;

            if (envVer.Major == 3 && envVer.Minor == 0)
            {
                return DotnetCoreVersion.netcoreapp30;
            }

            if (envVer.Major == 3 && envVer.Minor == 1)
            {
                return DotnetCoreVersion.netcoreapp31;
            }

            if (envVer.Major == 7)
            {
                return DotnetCoreVersion.net7;
            }

            if (envVer.Major == 6)
            {
                return DotnetCoreVersion.net6;
            }

            if (envVer.Major == 5)
            {
                return DotnetCoreVersion.net5;
            }

            if (envVer.Major == 4)
            {
                return DotnetCoreVersion.LessThan30;
            }

            return DotnetCoreVersion.Other;
        }
#endif
    }
}
