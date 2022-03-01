// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Logging
{
    public class SerilogWrapper : IWrapper
    {
        private const string AssemblyName = "Serilog";
        private const string TypeName = "Serilog.Events.ScalarValue";

        private static Func<object, object> _getLogLevel;
        private static Func<object, DateTimeOffset> _getTimestamp;
        private static Func<object, IDictionary> _getProperties;
        private static Func<string, object> _createScalarValue;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "serilog";

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
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];

            RecordLogMessage(logEvent, agent);

            DecorateLogMessage(logEvent, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, IAgent agent)
        {
            var getLogLevelFunc = _getLogLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEvent.GetType(), "Level");
            var logLevel = getLogLevelFunc(logEvent).ToString(); // Level is an enum so ToString() works.

            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTimeOffset>(logEvent.GetType(), "Timestamp");
            var timestamp = getTimestampFunc(logEvent);

            var renderedMessage = ((dynamic)logEvent).RenderMessage();

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(WrapperName, timestamp.DateTime, logLevel, (string)renderedMessage, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }

        private void DecorateLogMessage(object logEvent, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
            {
                return;
            }

            // has to be the field since property is IReadOnlyDictionary
            var getProperties = _getProperties ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<IDictionary>(logEvent.GetType(), "_properties");
            var propertiesDictionary = getProperties(logEvent);
            if (propertiesDictionary == null)
            {
                return;
            }

            // capture the constructor of the ScalarValue class.
            var createScalarValue = _createScalarValue ??= VisibilityBypasser.Instance.GenerateTypeFactory<string>(AssemblyName, TypeName);

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // uses underscores to support other frameworks that do not allow hyphens (Serilog)
            propertiesDictionary["NR_LINKING"] = createScalarValue(formattedMetadata);
        }
    }
}
