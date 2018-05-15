using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface INativeMethods
	{
		void ShutdownNativeThreadProfiler();

		FidTypeMethodName[] GetFunctionInfo(UIntPtr[] functionIDs);

		ThreadSnapshot[] GetProfileWithRelease(out Int32 hr);

		int InstrumentationRefresh();

		int AddCustomInstrumentation(string fileName, string xml);

		int ApplyCustomInstrumentation();
	}
}
