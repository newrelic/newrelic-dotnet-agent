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
        private const string DispatchName = "Dispatch";

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
            var frame7 = new System.Diagnostics.StackFrame(7, false).GetMethod().Name;
            string potentialDispatchFrame;
            if (frame7 != DispatchName)
            {
                // Frame 7 is not Dispatch. This means that an extra frame was in the stack called "UnsafeInvokeInternal" at frame 6
                // Look at frame 9 for duplicates instead of at frame 8
                potentialDispatchFrame = new System.Diagnostics.StackFrame(9, false).GetMethod().Name;
            }
            else
            {
                // Frame 7 is Dispatch, no extra frames
                // Look at frame 8 for duplicates
                potentialDispatchFrame = new System.Diagnostics.StackFrame(8, false).GetMethod().Name;
            }

            if (potentialDispatchFrame == DispatchName)
            {
                return Delegates.NoOp;
            }

            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];

            RecordLogMessage(logEvent, agent);

            DecorateLogMessage(logEvent, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, IAgent agent)
        {
            var getLogLevelFunc = _getLogLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEvent.GetType(), "Level");

            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTimeOffset>(logEvent.GetType(), "Timestamp");
            Func<object, DateTime> getDateTimeFunc = (logEvent) => getTimestampFunc(logEvent).UtcDateTime;

            Func<object, string> getMessageFunc = (logEvent) => ((dynamic)logEvent).RenderMessage();

            // This will either add the log message to the transaction or directly to the aggregator
            var xapi = agent.GetExperimentalApi();
            xapi.RecordLogMessage(WrapperName, logEvent, getDateTimeFunc, getLogLevelFunc, getMessageFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
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
