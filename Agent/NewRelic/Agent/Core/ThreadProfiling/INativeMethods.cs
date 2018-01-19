using System;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	public interface INativeMethods
	{
		// Client interface for requesting a function name by id
		void RequestFunctionNames(ulong[] functionIds, IntPtr callback);

		void RequestProfile(IntPtr successCallback, IntPtr failureCallback, IntPtr completeCallback);

		int InstrumentationRefresh();

		int AddCustomInstrumentation(string fileName, string xml);

		int ApplyCustomInstrumentation();
	}
}
