// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using Serilog;

namespace NewRelic.Providers.Wrapper.SerilogLogging
{
    public class SerilogCreateLoggerWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "SerilogCreateLoggerWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {

            var loggerConfiguration = instrumentedMethodCall.MethodCall.InvocationTarget as LoggerConfiguration;

            loggerConfiguration.WriteTo.Sink(new NewRelicSerilogSink(agent));

            return Delegates.NoOp;
        }
    }
}
