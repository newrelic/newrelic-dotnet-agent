// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Runtime.InteropServices;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core
{

    public class LinuxNativeMethods : INativeMethods
    {
        private const string DllName = "NewRelicProfiler";

        [DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ExternInstrumentationRefresh();

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
    }

    public class WindowsNativeMethods : INativeMethods
    {
        private const string DllName = "NewRelic.Profiler.dll";

        [DllImport(DllName, EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ExternInstrumentationRefresh();

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
    }
}
