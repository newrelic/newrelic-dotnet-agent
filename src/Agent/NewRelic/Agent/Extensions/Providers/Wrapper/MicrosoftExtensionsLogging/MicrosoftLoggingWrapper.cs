﻿// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MEL = Microsoft.Extensions.Logging;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using NewRelic.Reflection;
using System.Dynamic;

namespace NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging
{
    public class MicrosoftLoggingWrapper : IWrapper
    {
        // Cached accessor functions
        private static Func<object, dynamic> _getLoggersArray;
        private static Func<object, object> _getLoggerProperty;
        private static Func<object, MEL.IExternalScopeProvider> _getScopeProvider;

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
                Func<object, Dictionary<string, object>> getContextDataFunc = nothx => GetContextData(logger);

                var xapi = agent.GetExperimentalApi();
                xapi.RecordLogMessage(WrapperName, methodCall, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, getContextDataFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
            }
        }

        private static Dictionary<string, object> GetContextData(MEL.ILogger logger)
        {
            // We are trying to access this property:
            // logger.Loggers[0].Logger.ScopeProvider

            // Get the array of Loggers (logger.Loggers[])
            var getLoggersArrayFunc = _getLoggersArray ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<dynamic>(logger.GetType(), "Loggers");
            var loggers = getLoggersArrayFunc(logger);

            // Get the first logger in the array (logger.Loggers[0])
            object firstLogger = loggers.GetValue(0);

            // Get the internal logger (logger.Loggers[0].Logger)
            var getLoggerPropertyFunc = _getLoggerProperty ??= firstLogger.GetType().GetProperty("Logger").GetValue;
            object internalLogger = getLoggerPropertyFunc(firstLogger);

            // Get the scope provider from that logger (logger.Loggers[0].Logger.ScopeProvider)
            var getScopeProviderFunc = _getScopeProvider ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<MEL.IExternalScopeProvider>(internalLogger.GetType(), "ScopeProvider");
            var scopeProvider = getScopeProviderFunc(internalLogger);

            // Get the context data
            var harvestedKvps = new Dictionary<string, object>();
            scopeProvider.ForEachScope((scopeObject, accumulatedKvps) =>
            {
                if (scopeObject is IEnumerable<KeyValuePair<string, object>> kvps)
                {
                    foreach (var kvp in kvps)
                    {
                        accumulatedKvps.Add(kvp.Key, kvp.Value);
                    }
                }
                else if (scopeObject is KeyValuePair<string, object> kvp)
                {
                    accumulatedKvps.Add(kvp.Key, kvp.Value);
                }
                // Possibly handle case of IEnumerable<KeyValuePair<object, object>>, etc (not now though)
            }, harvestedKvps);

            return harvestedKvps;
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

            // uses the formatted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // get the handle so we can end the scope properly
            var handle = logger.BeginScope(new Dictionary<string, string>()
            {
                // using an underscore here to ensure we support serilog
                { "NR_LINKING", formattedMetadata }
            });

            return Delegates.GetDelegateFor(onComplete: () => handle?.Dispose());
        }
    }
}
