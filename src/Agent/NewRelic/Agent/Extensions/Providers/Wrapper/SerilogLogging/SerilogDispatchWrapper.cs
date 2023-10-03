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
    public class SerilogDispatchWrapper : IWrapper
    {
        private const string AssemblyName = "Serilog";
        private const string TypeName = "Serilog.Events.ScalarValue";
        private const string NrLinkingString = "NR_LINKING";

        private static Func<object, IDictionary> _getProperties;
        private static Func<string, object> _createScalarValue;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "SerilogDispatchWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            if (agent.Configuration.LogDecoratorEnabled)
            {
                var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];

                DecorateLogMessage(logEvent, agent);
            }

            return Delegates.NoOp;
        }

        private void DecorateLogMessage(object logEvent, IAgent agent)
        {
            // has to be the field since property is IReadOnlyDictionary
            var getProperties = _getProperties ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<IDictionary>(logEvent.GetType(), "_properties");
            var propertiesDictionary = getProperties(logEvent);
            if (propertiesDictionary == null || propertiesDictionary.Contains(NrLinkingString))
            {
                return;
            }

            // capture the constructor of the ScalarValue class.
            var createScalarValue = _createScalarValue ??= VisibilityBypasser.Instance.GenerateTypeFactory<string>(AssemblyName, TypeName);

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // uses underscores to support other frameworks that do not allow hyphens (Serilog)
            propertiesDictionary[NrLinkingString] = createScalarValue(formattedMetadata);
        }
    }
}
