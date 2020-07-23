using System;
using JetBrains.Annotations;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// Holds method invocation arguments.
    /// </summary>
    public class MethodCall
    {
        public readonly Method Method;
        [CanBeNull]
        public readonly Object InvocationTarget;
        [NotNull]
        public readonly Object[] MethodArguments;

        public MethodCall(Method method, [CanBeNull] Object invocationTarget, [NotNull] Object[] methodArguments)
        {
            Method = method;
            InvocationTarget = invocationTarget;
            MethodArguments = methodArguments ?? new Object[0];
        }
    }
}
