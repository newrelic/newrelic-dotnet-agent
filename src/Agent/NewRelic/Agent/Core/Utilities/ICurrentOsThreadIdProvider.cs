// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.Utilities;

/// <summary>
/// Returns the calling thread's OS-level thread id. Used to key a managed-side
/// (OS TID -> Thread.ManagedThreadId) map -- the profiling API cannot resolve that
/// mapping natively (see ManagedThreadIdRegistry), so a thread must report its own OS TID
/// alongside its own Thread.ManagedThreadId.
/// </summary>
public interface ICurrentOsThreadIdProvider
{
    long GetCurrentOsThreadId();
}

public class CurrentOsThreadIdProvider : ICurrentOsThreadIdProvider
{
    // SYS_gettid differs per architecture; these are the three architectures .NET supports on Linux.
    private const long SysGettidX64 = 186;
    private const long SysGettidArm = 224;
    private const long SysGettidArm64 = 178;

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    // glibc >= 2.30 exports gettid() directly, but this agent's minimum-supported distros
    // (e.g. RHEL 8, glibc 2.28) predate that, so go through the stable syscall(2) interface instead,
    // which has been present since Linux 2.4.11 with no glibc-version dependency.
    // Uses IntPtr for parameter/return to correctly handle 32-bit (4-byte) vs 64-bit (8-byte) C long widths.
    [DllImport("libc", EntryPoint = "syscall")]
    private static extern IntPtr SysCall(IntPtr number);

    public long GetCurrentOsThreadId()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetCurrentThreadId();
        }

        return (long)SysCall((IntPtr)GetSysGettidNumber());
    }

    private static long GetSysGettidNumber()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => SysGettidX64,
            Architecture.Arm => SysGettidArm,
            Architecture.Arm64 => SysGettidArm64,
            _ => throw new PlatformNotSupportedException($"Unsupported architecture for gettid: {RuntimeInformation.ProcessArchitecture}")
        };
    }
}
