// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging
{
    public class MicrosoftLoggingWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "MicrosoftLoggingWrapper";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            // There is no LogEvent equivilent in MSE Logging
            RecordLogMessage(instrumentedMethodCall.MethodCall, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(MethodCall methodCall, IAgent agent)
        {
            // MSE Logging doesn't have a timestamp for us to pull so we fudge it here.
            Func<object, DateTime> getTimestampFunc = mc => DateTime.UtcNow;

            Func<object, object> getLogLevelFunc = mc => ((MethodCall)mc).MethodArguments[0];

            Func<object, string> getRenderedMessageFunc = mc => ((MethodCall)mc).MethodArguments[2].ToString();

            var xapi = agent.GetExperimentalApi();

            xapi.RecordLogMessage(WrapperName, methodCall, getTimestampFunc, getLogLevelFunc, getRenderedMessageFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
        }
    }
}
