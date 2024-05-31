// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.AwsLambda
{
    public class OpenTracingWrapper : IWrapper
    {
        private const string WrapperName = "OpenTracingWrapper";

        public bool IsTransactionRequired => false;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            agent.Logger.Log(Agent.Extensions.Logging.Level.Warn, "OpenTracing is not compatible with the full .NET Agent and may result in undefined behavior. Please remove OpenTracing from your application.");
            return Delegates.NoOp;
        }
    }
}
