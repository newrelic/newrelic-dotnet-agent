// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Utilization;
using NewRelic.Core.Logging;
using NewRelic.SystemInterfaces;
using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Core.Utilities
{
    public class SystemInfo : ISystemInfo
    {
        private IDnsStatic _dnsStatic;

        private const int ExpectedBootIdLength = 36;

        private const int AsciiMaxValue = 127;


        public SystemInfo(IDnsStatic dnsStatic)
        {
            _dnsStatic = dnsStatic;
        }

        public ulong? GetTotalPhysicalMemoryBytes()
        {
            var isLinux = false;
#if NETSTANDARD2_0
            isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
#endif
            if (isLinux)
            {
                try
                {
                    var memInfo = File.ReadAllText("/proc/meminfo");
                    var memTotalRegex = new Regex(@"MemTotal\:\s*(\d+)\ kB");
                    var match = memTotalRegex.Match(memInfo);
                    if (match.Success)
                    {
                        // Despite the text of meminfo showing everything as 'kB' which should be
                        // 'kilobytes', i.e. 1000 bytes, in fact it is reporting 'kibibytes' (KiB, 1024 bytes)
                        var memTotalKiB = ulong.Parse(match.Groups[1].Value);
                        return memTotalKiB * 1024;
                    }
                    return null;
                }
                catch (Exception ex)
                {
                    Log.Warn(ex, "GetTotalPhysicalMemoryBytes(): exception caught trying to read from /proc/meminfo");
                    return null;
                }
            }
            else
            {
                try
                {
                    MemoryStatus status = new MemoryStatus();
                    status.length = Marshal.SizeOf(status);
                    if (!GlobalMemoryStatusEx(ref status))
                    {
                        int err = Marshal.GetLastWin32Error();
                        throw new Win32Exception(err);
                    }
                    return status.totalPhys;
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public int? GetTotalLogicalProcessors()
        {
            try
            {
                return System.Environment.ProcessorCount;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public BootIdResult GetBootId()
        {

#if NETSTANDARD2_0

			bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

			if (isLinux)
			{
				string bootId;

				try
				{
					var lines = File.ReadAllLines("/proc/sys/kernel/random/boot_id");
					bootId = lines.Length > 0 ? lines[0] : null;
				}
				catch (Exception ex)
				{
					Log.Warn(ex, "boot_id not found.");
					return new BootIdResult(null, false);
				}

				return new BootIdResult(bootId, ValidateBootId(bootId));
			}
#endif

            return new BootIdResult(null, true);
        }

        private bool ValidateBootId(string bootId)
        {
            return (bootId != null) && IsAsciiString(bootId) && (bootId.Length == ExpectedBootIdLength);
        }

        private static bool IsAsciiString(string inputString)
        {
            if (inputString.Length == 0)
                return false;

            for (int i = 0; i < inputString.Length; i++)
            {
                if (inputString[i] > AsciiMaxValue)
                {
                    return false;
                }
            }
            return true;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryStatus
        {
            public int length;
            private readonly int memoryLoad;
            public readonly ulong totalPhys;
            private readonly ulong availPhys;
            private readonly ulong totalPageFile;
            private readonly ulong availPageFile;
            private readonly ulong totalVirtual;
            private readonly ulong availVirtual;
            private readonly ulong availExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
    }
}
