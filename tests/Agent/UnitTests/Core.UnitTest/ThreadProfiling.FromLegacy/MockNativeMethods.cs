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
    }
}
