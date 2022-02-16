// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
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
        private static Func<object, DateTime> _getTimestamp;
        private static Func<object, IDictionary> _getProperties;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "log4net";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];

            RecordLogMessage(logEvent, agent);

            DecorateLogMessage(logEvent, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, IAgent agent)
        {
            var getLogLevelFunc = _getLogLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEvent.GetType(), "Level");
            var logLevel = getLogLevelFunc(logEvent).ToString(); // Level class has a ToString override we can use.

            // RenderedMessage is get only
            var getRenderedMessageFunc = _getRenderedMessage ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(logEvent.GetType(), "RenderedMessage");
            var renderedMessage = getRenderedMessageFunc(logEvent);

            // We can either get this in Local or UTC
            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTime>(logEvent.GetType(), "TimeStampUtc");
            var timestamp = getTimestampFunc(logEvent);

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(timestamp, logLevel, renderedMessage, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }

        private void DecorateLogMessage(object logEvent, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
            {
                return;
            }

            var getProperties = _getProperties ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary>(logEvent.GetType(), "Properties");
            var propertiesDictionary = getProperties(logEvent);

            if (propertiesDictionary == null)
            {
                return;
            }

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // uses underscores to support other frameworks that do not allow hyphens (Serilog)
            propertiesDictionary["NR_LINKING_METADATA"] = formattedMetadata;
        }
    }
}
