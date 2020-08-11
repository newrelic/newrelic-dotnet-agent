// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
    public interface IWrapper
    {
        /// <summary>
        /// Called once per method per AppDomain to determine whether or not this wrapper can wrap the given instrumented method info. Returns a response struct that contains a boolean and a string. The boolean is true if the provided method is one that this wrapper knows how to handle, false otherwise. The string optionally contains null or any additional information that may explain why the boolean was true or false.
        /// </summary>
        /// <param name="instrumentedMethodInfo">Details about the method and wrapper that is being wrapped.</param>
        /// <returns>A response struct that contains a boolean and a string. The boolean is true if the provided method is one that this wrapper knows how to handle, false otherwise. The string optionally contains null or any additional information that may explain why the boolean was true or false.</returns>
        CanWrapResponse CanWrap(InstrumentedMethodInfo instrumentedMethodInfo);

        /// <summary>
        /// Performs work before a wrapped method call and returns a delegate containing work to perform after the wrapped method call.
        /// </summary>
        /// <param name="instrumentedMethodCall">The method call being wrapped, plus any instrumentation options.</param>
        /// <param name="agent">The API that wrappers can use to talk to the agent.</param>
        /// <param name="transaction">The current transaction or null if IsTransactionRequired is false</param>
        AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction);

        /// <summary>
        /// Returns true if this wrapper requires a transaction.  If it does, BeforeWrappedMethod will not be invoked
        /// when a wrapper is requested and there is no current transaction.
        /// </summary>
        /// <returns></returns>
        bool IsTransactionRequired { get; }
    }
}
