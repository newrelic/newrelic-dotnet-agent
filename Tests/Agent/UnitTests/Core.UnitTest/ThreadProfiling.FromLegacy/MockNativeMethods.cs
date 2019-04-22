using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public class MockNativeMethods : INativeMethods
	{

		public void RequestFunctionNames(ulong[] functionId, IntPtr callback)
		{
		}

		public void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback)
		{
		}

		public void ShutdownNativeThreadProfiler()
		{
		}

		public FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs)
		{
			return new FidTypeMethodName[0];
		}

		public ThreadSnapshot[] GetProfileWithRelease(out Int32 hr)
		{
			hr = 0;
			return new ThreadSnapshot[0];
		}

		public int InstrumentationRefresh()
		{
			return 0;
		}

		public int AddCustomInstrumentation(string fileName, string xml)
		{
			return 0;
		}

		public int ApplyCustomInstrumentation()
		{
			return 0;
		}
	}
}
