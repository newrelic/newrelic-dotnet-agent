using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    public class NativeMethods : INativeMethods
    {
        [DllImportAttribute("NewRelicProfiler", EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
        public static extern int InstrumentationRefresh();

        [DllImportAttribute("NewRelicProfiler", EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ExternRequestFunctionNames(ulong[] functionIds, int length, IntPtr callback);

        [DllImportAttribute("NewRelicProfiler", EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ExternRequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);

        public void RequestFunctionNames(ulong[] functionIds, IntPtr callback)
        {
            if (functionIds.Length == 0)
            {
                return;
            }
            ExternRequestFunctionNames(functionIds, functionIds.Length, callback);
        }

        public void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback)
        {
            ExternRequestProfile(successCallback, failureCallback, completeCallback);
        }
    }

    public class WindowsNativeMethods : INativeMethods
    {
        [DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
        public static extern int InstrumentationRefresh();

        [DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
        public static extern void ExternRequestFunctionNames(ulong[] functionIds, int length, IntPtr callback);

        [DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
        private static extern void ExternRequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);

        public void RequestFunctionNames(ulong[] functionIds, IntPtr callback)
        {
            if (functionIds.Length == 0)
            {
                return;
            }
            ExternRequestFunctionNames(functionIds, functionIds.Length, callback);
        }

        public void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback)
        {
            ExternRequestProfile(successCallback, failureCallback, completeCallback);
        }
    }
}
