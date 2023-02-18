// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;
using NLog;

namespace NewRelic.Providers.Wrapper.NLogLogging
{
    public class NLogWrapper : IWrapper
    {
        private static Action<object, string> _setFormattedMessage;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "nlog";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            // Since NLog can alter the messages directly, we need to move the MEL check
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[2] as LogEventInfo;
            var logEventType = typeof(LogEventInfo);

            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.NLog])
            {
                RecordLogMessage(logEvent, logEventType, agent);
            }

            // We want this to happen instead of MEL so no provider check here.
            DecorateLogMessage(logEvent, logEventType, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(LogEventInfo logEvent, Type logEventType, IAgent agent)
        {
            Func<object, object> getLevelFunc = le => ((LogEventInfo)le).Level;

            Func<object, string> getRenderedMessageFunc = le => ((LogEventInfo)le).FormattedMessage;

            Func<object, DateTime> getTimestampFunc = le => ((LogEventInfo)le).TimeStamp;

            Func<object, Exception> getLogExceptionFunc = le => ((LogEventInfo)le).Exception;

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(WrapperName, logEvent, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, GetContextData, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }

        private void DecorateLogMessage(LogEventInfo logEvent, Type logEventType, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled || string.IsNullOrWhiteSpace(logEvent?.FormattedMessage))
            {
                return;
            }

            var setFormattedMessage = _setFormattedMessage ??= VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(logEventType, "_formattedMessage");
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);
            setFormattedMessage(logEvent, logEvent.FormattedMessage + " " + formattedMetadata);
        }

        private Dictionary<string, object> GetContextData(object logEvent)
        {
            var contextData = new Dictionary<string, object>();
            foreach (var property in ((LogEventInfo)logEvent).Properties)
            {
                contextData[property.Key.ToString()] = property.Value;
            }

            return contextData;
        }
    }
}
