﻿using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Utilization;
using NewRelic.SystemInterfaces;

namespace NewRelic.Agent.Core.Utilities
{
	public class SystemInfo : ISystemInfo
	{
		private IDnsStatic _dnsStatic;

		private const int ExpectedBootIdLength = 36;

		private const int AsciiMaxValue = 127;


		public SystemInfo([NotNull] IDnsStatic dnsStatic)
		{
			_dnsStatic = dnsStatic;
		}

		public UInt64 GetTotalPhysicalMemoryBytes()
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
				return 0;
			}
		}

		public Int32 GetTotalLogicalProcessors()
		{
			return System.Environment.ProcessorCount;
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
					Log.Warn("boot_id not found. " + ex.Message);
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
			public Int32 length;
			private readonly Int32 memoryLoad;
			public readonly UInt64 totalPhys;
			private readonly UInt64 availPhys;
			private readonly UInt64 totalPageFile;
			private readonly UInt64 availPageFile;
			private readonly UInt64 totalVirtual;
			private readonly UInt64 availVirtual;
			private readonly UInt64 availExtendedVirtual;
		}

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
	}
}
