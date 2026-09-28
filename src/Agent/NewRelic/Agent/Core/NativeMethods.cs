// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Core;

public class LinuxNativeMethods : INativeMethods
{
    private const string DllName = "NewRelicProfiler";

    [DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternInstrumentationRefresh();

    [DllImport(DllName, EntryPoint = "ReloadConfiguration", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternReloadConfiguration();

    [DllImport(DllName, EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

    [DllImport(DllName, EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternApplyCustomInstrumentation();

    public int InstrumentationRefresh()
    {
        try
        {
            return ExternInstrumentationRefresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LinuxNativeMethods.InstrumentationRefresh() exception");
            return -1;
        }
    }

    public int ReloadConfiguration()
    {
        try
        {
            return ExternReloadConfiguration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "LinuxNativeMethods.ReloadConfiguration() exception");
            return -1;
        }
    }

    public int AddCustomInstrumentation(string fileName, string xml)
    {
        return ExternAddCustomInstrumentation(fileName, xml);
    }

    public int ApplyCustomInstrumentation()
    {
        return ExternApplyCustomInstrumentation();
    }

    [DllImport(DllName, EntryPoint = "ShutdownThreadProfiler", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ExternShutdownThreadProfiler();

    [DllImport(DllName, EntryPoint = "ReleaseProfile", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ExternReleaseProfile();

    [DllImport(DllName, EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternRequestProfile([Out] out IntPtr snapshots, [Out] out int length);

    [DllImport(DllName, EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternRequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);

    public void ReleaseProfile()
    {
        ExternReleaseProfile();
    }

    public int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo)
    {
        return ExternRequestFunctionNames(functionIds, length, out functionInfo);
    }

    public int RequestProfile([Out] out IntPtr snapshots, [Out] out int length)
    {
        return ExternRequestProfile(out snapshots, out length);
    }

    public void ShutdownNativeThreadProfiler()
    {
        ExternShutdownThreadProfiler();
    }

    [DllImport(DllName, EntryPoint = "ContinuousProfilerStart", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerStart(int intervalMs);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerStop", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerStop();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerReadThreadSamples", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerReadThreadSamples(int len, byte[] buffer);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerSetTraceContext", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerResetTraceContext", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerResetTraceContext();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerRetireSpans", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerRetireSpans(long[] spanIds, int count);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerGetPendingPushCell", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerGetPendingPushCell([Out] out IntPtr cell);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerSetAgentWork", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerSetAgentWork();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerResetAgentWork", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerResetAgentWork();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerShutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerShutdown();

    public int ContinuousProfilerStart(int intervalMs)
    {
        return ExternContinuousProfilerStart(intervalMs);
    }

    public int ContinuousProfilerStop()
    {
        return ExternContinuousProfilerStop();
    }

    public int ContinuousProfilerReadThreadSamples(int len, byte[] buffer)
    {
        return ExternContinuousProfilerReadThreadSamples(len, buffer);
    }

    public int ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId)
    {
        return ExternContinuousProfilerSetTraceContext(traceIdHigh, traceIdLow, spanId);
    }

    public int ContinuousProfilerResetTraceContext()
    {
        return ExternContinuousProfilerResetTraceContext();
    }

    public int ContinuousProfilerRetireSpans(long[] spanIds, int count)
    {
        return ExternContinuousProfilerRetireSpans(spanIds, count);
    }

    public int ContinuousProfilerGetPendingPushCell(out IntPtr cell)
    {
        return ExternContinuousProfilerGetPendingPushCell(out cell);
    }

    public int ContinuousProfilerSetAgentWork()
    {
        return ExternContinuousProfilerSetAgentWork();
    }

    public int ContinuousProfilerResetAgentWork()
    {
        return ExternContinuousProfilerResetAgentWork();
    }

    public int ContinuousProfilerShutdown()
    {
        return ExternContinuousProfilerShutdown();
    }
}

public class WindowsNativeMethods : INativeMethods
{
    private const string DllName = "NewRelic.Profiler.dll";

    [DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternInstrumentationRefresh();

    [DllImport(DllName, EntryPoint = "ReloadConfiguration", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternReloadConfiguration();

    [DllImport(DllName, EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

    [DllImport(DllName, EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternApplyCustomInstrumentation();

    public int InstrumentationRefresh()
    {
        try
        {
            return ExternInstrumentationRefresh();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WindowsNativeMethods.InstrumentationRefresh() exception");
            return -1;
        }
    }

    public int ReloadConfiguration()
    {
        try
        {
            return ExternReloadConfiguration();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WindowsNativeMethods.ReloadConfiguration() exception");
            return -1;
        }
    }

    public int AddCustomInstrumentation(string fileName, string xml)
    {
        return ExternAddCustomInstrumentation(fileName, xml);
    }

    public int ApplyCustomInstrumentation()
    {
        return ExternApplyCustomInstrumentation();
    }


    [DllImport(DllName, EntryPoint = "ShutdownThreadProfiler", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ExternShutdownThreadProfiler();

    [DllImport(DllName, EntryPoint = "ReleaseProfile", CallingConvention = CallingConvention.Cdecl)]
    private static extern void ExternReleaseProfile();

    [DllImport(DllName, EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternRequestProfile([Out] out IntPtr snapshots, [Out] out int length);

    [DllImport(DllName, EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternRequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo);

    public void ReleaseProfile()
    {
        ExternReleaseProfile();
    }

    public int RequestFunctionNames(UIntPtr[] functionIds, int length, [Out] out IntPtr functionInfo)
    {
        return ExternRequestFunctionNames(functionIds, length, out functionInfo);
    }

    public int RequestProfile([Out] out IntPtr snapshots, [Out] out int length)
    {
        return ExternRequestProfile(out snapshots, out length);
    }

    public void ShutdownNativeThreadProfiler()
    {
        ExternShutdownThreadProfiler();
    }

    [DllImport(DllName, EntryPoint = "ContinuousProfilerStart", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerStart(int intervalMs);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerStop", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerStop();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerReadThreadSamples", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerReadThreadSamples(int len, byte[] buffer);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerSetTraceContext", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerResetTraceContext", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerResetTraceContext();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerRetireSpans", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerRetireSpans(long[] spanIds, int count);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerGetPendingPushCell", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerGetPendingPushCell([Out] out IntPtr cell);

    [DllImport(DllName, EntryPoint = "ContinuousProfilerSetAgentWork", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerSetAgentWork();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerResetAgentWork", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerResetAgentWork();

    [DllImport(DllName, EntryPoint = "ContinuousProfilerShutdown", CallingConvention = CallingConvention.Cdecl)]
    private static extern int ExternContinuousProfilerShutdown();

    public int ContinuousProfilerStart(int intervalMs)
    {
        return ExternContinuousProfilerStart(intervalMs);
    }

    public int ContinuousProfilerStop()
    {
        return ExternContinuousProfilerStop();
    }

    public int ContinuousProfilerReadThreadSamples(int len, byte[] buffer)
    {
        return ExternContinuousProfilerReadThreadSamples(len, buffer);
    }

    public int ContinuousProfilerSetTraceContext(long traceIdHigh, long traceIdLow, long spanId)
    {
        return ExternContinuousProfilerSetTraceContext(traceIdHigh, traceIdLow, spanId);
    }

    public int ContinuousProfilerResetTraceContext()
    {
        return ExternContinuousProfilerResetTraceContext();
    }

    public int ContinuousProfilerRetireSpans(long[] spanIds, int count)
    {
        return ExternContinuousProfilerRetireSpans(spanIds, count);
    }

    public int ContinuousProfilerGetPendingPushCell(out IntPtr cell)
    {
        return ExternContinuousProfilerGetPendingPushCell(out cell);
    }

    public int ContinuousProfilerSetAgentWork()
    {
        return ExternContinuousProfilerSetAgentWork();
    }

    public int ContinuousProfilerResetAgentWork()
    {
        return ExternContinuousProfilerResetAgentWork();
    }

    public int ContinuousProfilerShutdown()
    {
        return ExternContinuousProfilerShutdown();
    }
}
