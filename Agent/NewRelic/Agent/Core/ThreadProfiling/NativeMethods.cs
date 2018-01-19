using System;
using System.Runtime.InteropServices;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public class NativeMethods : INativeMethods
	{
		[DllImportAttribute("NewRelicProfiler", EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternInstrumentationRefresh();

		[DllImportAttribute("NewRelicProfiler", EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
		public static extern void ExternRequestFunctionNames(ulong[] functionIds, int length, IntPtr callback);

		[DllImportAttribute("NewRelicProfiler", EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
		private static extern void ExternRequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);

		[DllImportAttribute("NewRelicProfiler", EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

		[DllImportAttribute("NewRelicProfiler", EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternApplyCustomInstrumentation();

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

		public int InstrumentationRefresh()
		{
			return ExternInstrumentationRefresh();
		}

		public int AddCustomInstrumentation(string fileName, string xml)
		{
			return ExternAddCustomInstrumentation(fileName, xml);
		}

		public int ApplyCustomInstrumentation()
		{
			return ExternApplyCustomInstrumentation();
		}
	}

	public class WindowsNativeMethods : INativeMethods
	{
		[DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "InstrumentationRefresh", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternInstrumentationRefresh();

		[DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "RequestFunctionNames", CallingConvention = CallingConvention.Cdecl)]
		public static extern void ExternRequestFunctionNames(ulong[] functionIds, int length, IntPtr callback);

		[DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "RequestProfile", CallingConvention = CallingConvention.Cdecl)]
		private static extern void ExternRequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);

		[DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "AddCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternAddCustomInstrumentation(string fileName, string xml);

		[DllImportAttribute("NewRelic.Profiler.dll", EntryPoint = "ApplyCustomInstrumentation", CallingConvention = CallingConvention.Cdecl)]
		private static extern int ExternApplyCustomInstrumentation();

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

		public int InstrumentationRefresh()
		{
			return ExternInstrumentationRefresh();
		}

		public int AddCustomInstrumentation(string fileName, string xml)
		{
			return ExternAddCustomInstrumentation(fileName, xml);
		}

		public int ApplyCustomInstrumentation()
		{
			return ExternApplyCustomInstrumentation();
		}
	}
}
