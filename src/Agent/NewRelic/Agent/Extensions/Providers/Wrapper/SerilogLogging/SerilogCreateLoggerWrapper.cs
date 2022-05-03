// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.SerilogLogging
{
    public class SerilogCreateLoggerWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "SerilogCreateLoggerWrapper";
        private Func<object, IList> _getLogEventSinks;

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.Serilog])
            {
                return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var configurationLoader = instrumentedMethodCall.MethodCall.InvocationTarget;
            var getLogEventSinks = _getLogEventSinks ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<IList>("Serilog", "Serilog.LoggerConfiguration", "_logEventSinks");

            var logEventSinks = getLogEventSinks.Invoke(configurationLoader);
            logEventSinks.Add(new CustomSink(agent));
            return Delegates.NoOp;
        }
    }
}
