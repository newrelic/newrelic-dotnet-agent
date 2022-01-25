// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Logging
{
    public class Log4netWrapper : IWrapper
    {
        private static Func<object, object> _getLogLevel;
        private static Func<object, string> _getRenderedMessage;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "log4net";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var loggingEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];

            var getLogLvelFunc = _getLogLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(loggingEvent.GetType(), "Level");
            var logLevel = getLogLvelFunc(loggingEvent).ToString(); // Level class has a ToString override we can use.

            // RenderedMessage is get only
            var getRenderedMessageFunc = _getRenderedMessage ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(loggingEvent.GetType(), "RenderedMessage");
            var renderedMessage = getRenderedMessageFunc(loggingEvent);

            var logLineSize = renderedMessage.Length * sizeof(Char);
            var xapi = agent.GetExperimentalApi();
            xapi.IncrementLogLinesCount(logLevel);
            xapi.UpdateLogSize(logLevel, logLineSize);

            return Delegates.NoOp;
        }
    }
}
