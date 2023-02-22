// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.NLogLogging
{
    public class NLogWrapper : IWrapper
    {
        private static Action<object, string> _setFormattedMessage;
        private static Func<object, object> _getLevel;
        private static Func<object, string> _getFormattedMessage;
        private static Func<object, DateTime> _getTimestamp;
        private static Func<object, Exception> _getLogException;
        private static Func<object, IDictionary<object, object>> _getPropertiesDictionary;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "nlog";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            // Since NLog can alter the messages directly, we need to move the MEL check
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[2];
            var logEventType = logEvent.GetType();

            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.NLog])
            {
                RecordLogMessage(logEvent, logEventType, agent);
            }

            // We want this to happen instead of MEL so no provider check here.
            DecorateLogMessage(logEvent, logEventType, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, Type logEventType, IAgent agent)
        {
            var getLevelFunc = _getLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEventType, "Level");

            var getRenderedMessageFunc = GetFormattedMessageFunc(logEventType); ;

            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTime>(logEventType, "TimeStamp");

            var getLogExceptionFunc = _getLogException ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Exception>(logEventType, "Exception");

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(WrapperName, logEvent, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, GetContextData, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }

        private void DecorateLogMessage(object logEvent, Type logEventType, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
            {
                return;
            }

            var getFormattedMessageFunc = GetFormattedMessageFunc(logEventType);
            var formattedMessage = getFormattedMessageFunc(logEvent);
            if (string.IsNullOrWhiteSpace(formattedMessage))
            {
                return;
            }

            // NLog version strings are not setup to allow using min/max version in instrumentation - they only report major version.
            // This will use the 4.5+ field name and if that is not found, use the pre4.5 field name.
            // Follow up calls will not throw and just work.
            Action<object, string> setFormattedMessage;
            if (_setFormattedMessage == null)
            {
                try
                {
                    setFormattedMessage = _setFormattedMessage ??= VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(logEventType, "_formattedMessage");
                }
                catch
                {
                    setFormattedMessage = _setFormattedMessage ??= VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(logEventType, "formattedMessage");
                }
            }
            else
            {
                setFormattedMessage = _setFormattedMessage;
            }

            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);
            setFormattedMessage(logEvent, formattedMessage + " " + formattedMetadata);
        }

        private Func<object, string> GetFormattedMessageFunc(Type logEventType)
        {
            return _getFormattedMessage ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(logEventType, "FormattedMessage");
        }

        private Dictionary<string, object> GetContextData(object logEvent)
        {
            var contextData = new Dictionary<string, object>();
            var getPropertiesDictionary = _getPropertiesDictionary ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary<object, object>>(logEvent.GetType(), "Properties");
            var properties = getPropertiesDictionary(logEvent);
            foreach (var property in properties)
            {
                contextData[property.Key.ToString()] = property.Value;
            }

            return contextData;
        }
    }
}
