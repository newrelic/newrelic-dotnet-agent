using System;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// Holds method invocation arguments.
    /// </summary>
    public class MethodCall
    {
        public readonly Method Method;
        public readonly Object InvocationTarget;
        public readonly Object[] MethodArguments;

        public MethodCall(Method method, Object invocationTarget, Object[] methodArguments)
        {
            Method = method;
            InvocationTarget = invocationTarget;
            MethodArguments = methodArguments ?? new Object[0];
        }
    }
}
