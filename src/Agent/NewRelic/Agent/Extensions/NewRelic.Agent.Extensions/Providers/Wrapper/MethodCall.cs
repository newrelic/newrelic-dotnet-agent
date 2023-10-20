// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    /// <summary>
    /// Holds method invocation arguments.
    /// </summary>
    public class MethodCall
    {
        public readonly Method Method;
        public readonly object InvocationTarget;
        public readonly object[] MethodArguments;
        public readonly bool IsAsync;

        public MethodCall(Method method, object invocationTarget, object[] methodArguments, bool isAsync)
        {
            Method = method;
            InvocationTarget = invocationTarget;
            MethodArguments = methodArguments ?? new object[0];
            IsAsync = isAsync;
        }
    }
}
