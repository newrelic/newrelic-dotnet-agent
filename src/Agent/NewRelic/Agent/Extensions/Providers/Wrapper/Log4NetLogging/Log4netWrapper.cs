// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Logging
{
    public class Log4netWrapper : IWrapper
    {
        private static Func<object, object> _getLevel;
        private static Func<object, string> _getRenderedMessage;
        private static Func<object, DateTime> _getTimestamp;
        private static Func<object, Exception> _getLogException;
        private static Func<object, IDictionary> _getProperties;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "log4net";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.Log4Net])
            {
                return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var logEventType = logEvent.GetType();

            RecordLogMessage(logEvent, logEventType, agent);

            DecorateLogMessage(logEvent, logEventType, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, Type logEventType, IAgent agent)
        {
            var getLevelFunc = _getLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEventType, "Level");

            // RenderedMessage is get only
            var getRenderedMessageFunc = _getRenderedMessage ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(logEventType, "RenderedMessage");

            // Older versions of log4net only allow access to a timestamp in local time
            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTime>(logEventType, "TimeStamp");

            var getLogExceptionFunc = _getLogException ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Exception>(logEventType, "ExceptionObject");

            // Placeholder until context data (custom attribute) instrumentation is implemented
            Func <object, Dictionary<string, object>> getContextDataFunc = (logEvent) => null;

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(WrapperName, logEvent, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, getContextDataFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }

        private void DecorateLogMessage(object logEvent, Type logEventType, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
            {
                return;
            }

            var getProperties = _getProperties ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary>(logEventType, "Properties");
            var propertiesDictionary = getProperties(logEvent);

            if (propertiesDictionary == null)
            {
                return;
            }

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // uses underscores to support other frameworks that do not allow hyphens (Serilog)
            propertiesDictionary["NR_LINKING"] = formattedMetadata;
        }
    }
}
