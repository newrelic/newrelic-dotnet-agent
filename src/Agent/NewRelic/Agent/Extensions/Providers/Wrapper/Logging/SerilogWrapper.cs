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
            var logLevel = getLogLevelFunc(logEvent).ToString(); // Level is an emums so ToString work.

            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTimeOffset>(logEvent.GetType(), "Timestamp");
            var timestamp = getTimestampFunc(logEvent);

            var renderedMessage = ((dynamic)logEvent).RenderMessage();

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(timestamp.DateTime, logLevel, (string)renderedMessage, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
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

            // the keys in the metadata match the ones used for decorating
            var metadata = agent.GetLinkingMetadata();
            foreach (var entry in metadata)
            {
                // Serilog does not support '.' in names, but does support underscore.
                propertiesDictionary[entry.Key.Replace('.','.')] = createScalarValue(entry.Value);
            }
        }
    }
}
