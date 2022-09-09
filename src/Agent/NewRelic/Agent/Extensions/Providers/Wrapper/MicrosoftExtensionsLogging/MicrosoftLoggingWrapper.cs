﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MEL = Microsoft.Extensions.Logging;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;

namespace NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging
{
    public class MicrosoftLoggingWrapper : IWrapper
    {
        public bool IsTransactionRequired => false;

        private const string WrapperName = "MicrosoftLogging";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var melLoggerInstance = (MEL.ILogger)instrumentedMethodCall.MethodCall.InvocationTarget;

            // There is no LogEvent equivalent in MSE Logging
            RecordLogMessage(instrumentedMethodCall.MethodCall, melLoggerInstance, agent);

            // need to return AfterWrappedMethodDelegate here since we have two different return options from this method.
            return DecorateLogMessage(melLoggerInstance, agent);
        }

        private void RecordLogMessage(MethodCall methodCall, MEL.ILogger logger, IAgent agent)
        {
            // We need to manually check if each log message is enabled since our MEL instrumentation takes place before
            // logs have been filtered to enabled levels.. Since this iterates all the loggers, cache responses for 60 seconds
            var logLevelIsEnabled = logger.IsEnabled((MEL.LogLevel)methodCall.MethodArguments[0]);

            if (logLevelIsEnabled)
            {
                // MSE Logging doesn't have a timestamp for us to pull so we fudge it here.
                Func<object, DateTime> getTimestampFunc = mc => DateTime.UtcNow;

                Func<object, string> getLevelFunc = mc => ((MethodCall)mc).MethodArguments[0].ToString();

                Func<object, string> getRenderedMessageFunc = mc => ((MethodCall)mc).MethodArguments[2].ToString();

                Func<object, Exception> getLogExceptionFunc = mc => ((MethodCall)mc).MethodArguments[3] as Exception; // using "as" since we want a null if missing

                var xapi = agent.GetExperimentalApi();

                xapi.RecordLogMessage(WrapperName, methodCall, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
            }
        }

        private AfterWrappedMethodDelegate DecorateLogMessage(MEL.ILogger logger, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
            {
                return Delegates.NoOp;
            }

            // NLog can alter the message so we want to skip MEL decoration for NLog
            if (LogProviders.RegisteredLogProvider[(int)LogProvider.NLog])
            {
                return Delegates.NoOp;
            }

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // get the handle so we can end the scope properly
            var handle = logger.BeginScope(new Dictionary<string, string>()
            {
                // using an underscore here to ensure we support serilog
                {"NR_LINKING", formattedMetadata}
            });

            return Delegates.GetDelegateFor(onComplete: () => handle?.Dispose());
        }
    }
}
