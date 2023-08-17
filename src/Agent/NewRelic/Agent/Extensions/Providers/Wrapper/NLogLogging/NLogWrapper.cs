// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Xml.Linq;
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
        private static Func<object, string> _messageGetter;
        private static Func<object, DateTime> _getTimestamp;
        private static Func<object, Exception> _getLogException;
        private static Func<object, IDictionary<object, object>> _getPropertiesDictionary;
        private static Func<IEnumerable<KeyValuePair<string, object>>> _getScopeData;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "nlog";
        private const string FormattedMessage45Plus = "_formattedMessage";
        private const string FormattedMessagePre45 = "formattedMessage";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            // Since NLog can alter the messages directly, we need to move the MEL check
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[2];
            var logEventType = logEvent.GetType();

            RecordLogMessage(logEvent, logEventType, agent);
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
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // This wrapper for NLog uses a belt-and-suspenders approach to decorating log output. We first try to decorate the Message property,
            // then get the FormattedMessage property and check to see if it is decorated. If not, decorate the FormattedMessage backing field directly.
            // Note: this still does not work for all log messages, particularly the messages output by ASP.NET Core when NLog.Web.AspNetCore is used.

            var messageGetter = _messageGetter ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(logEventType, "Message");

            var originalMessage = messageGetter(logEvent);

            var messageSetter = VisibilityBypasser.Instance.GeneratePropertySetter<string>(logEvent, "Message");

            messageSetter(originalMessage + " " + formattedMetadata);

            var getFormattedMessageFunc = GetFormattedMessageFunc(logEventType);
            var formattedMessage = getFormattedMessageFunc(logEvent);
            if (string.IsNullOrWhiteSpace(formattedMessage))
            {
                return;
            }

            if (LoggingHelpers.ContainsLinkingToken(formattedMessage))
            {
                return;
            }

            // NLog version strings are not setup to allow using min/max version in instrumentation - they only report major version.
            // This will default to the 4.5+ field name and if that is not found, use the pre4.5 field name.
            var formattedMessageName = FormattedMessage45Plus;
            if (_setFormattedMessage == null)
            {
                if (logEventType.GetField(FormattedMessage45Plus, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) == null)
                {
                    formattedMessageName = FormattedMessagePre45;
                }
            }

            var setFormattedMessage = _setFormattedMessage ??= VisibilityBypasser.Instance.GenerateFieldWriteAccessor<string>(logEventType, formattedMessageName);
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

            // NLog treats and stores Properties and Scope Context differently. If we need to add support for older versions of NLog, it's two calls instead of GetAllProperties():
            //    NLog.MappedDiagnosticsLogicalContext.GetNames()
            //       NLog.MappedDiagnosticsLogicalContext.Get(name)
            try
            {
                _getScopeData = _getScopeData ??= VisibilityBypasser.Instance.GenerateParameterlessStaticMethodCaller<IEnumerable<KeyValuePair<string, object>>>("NLog", "NLog.ScopeContext", "GetAllProperties");
            }
            catch
            {
                _getScopeData = () => new List<KeyValuePair<string, object>>();
            }
            var scopeData = _getScopeData();
            foreach (var pair in scopeData)
            {
                contextData[pair.Key] = pair.Value;
            }

            return contextData;
        }
    }
}
