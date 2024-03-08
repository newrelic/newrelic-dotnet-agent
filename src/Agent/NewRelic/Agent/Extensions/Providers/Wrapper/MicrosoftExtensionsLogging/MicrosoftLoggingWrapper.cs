// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using MEL = Microsoft.Extensions.Logging;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using NewRelic.Reflection;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace NewRelic.Providers.Wrapper.MicrosoftExtensionsLogging
{
    public class MicrosoftLoggingWrapper : IWrapper
    {
        // Cached accessor functions
        private static Func<object, dynamic> _getLoggersArray;
        private static PropertyInfo _scopeProviderPropertyInfo;

        private static bool _contextDataNotSupported = false;

        public bool IsTransactionRequired => false;

        private const string WrapperName = "MicrosoftLogging";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            if (!LogProviders.KnownMELProviderEnabled)
            {
                return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
            }
            return new CanWrapResponse(false);
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
                Func<object, Dictionary<string, object>> getContextDataFunc = nothx => GetContextData(logger, agent);

                var xapi = agent.GetExperimentalApi();
                xapi.RecordLogMessage(WrapperName, methodCall, getTimestampFunc, getLevelFunc, getRenderedMessageFunc, getLogExceptionFunc, getContextDataFunc, agent.TraceMetadata.SpanId, agent.TraceMetadata.TraceId);
            }
        }

        private static Dictionary<string, object> GetContextData(MEL.ILogger logger, IAgent agent)
        {
            if (_contextDataNotSupported) // short circuit if we previously got an exception trying to access context data
            {
                return null;
            }

            try
            {
                // MEL keeps an array of scope handlers. We can ask one of them for the current scope data.

                // Get the array of ScopeLoggers (logger.ScopeLoggers[])
                var getLoggersArrayFunc = _getLoggersArray ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<dynamic>(logger.GetType(), "ScopeLoggers");
                var loggers = getLoggersArrayFunc(logger);

                // Get the last ScopeLogger in the array (logger.ScopeLoggers[loggers.Length-1])
                // If there is more than one scope logger, the last logger is the one with the ExternalScopeProvider set
                object lastLogger = loggers.GetValue(loggers.Length-1);

                // Get the scope provider from that logger (logger.ScopeLoggers[loggers.Length-1].ExternalScopeProvider)
                var scopeProviderPI = _scopeProviderPropertyInfo ??= lastLogger.GetType().GetProperty("ExternalScopeProvider");
                var scopeProvider = scopeProviderPI.GetValue(lastLogger) as IExternalScopeProvider;

                // Get the context data
                var harvestedKvps = new Dictionary<string, object>();
                scopeProvider?.ForEachScope((scopeObject, accumulatedKvps) =>
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
            catch (Exception e)
            {
                agent.Logger.Log(Level.Warn, $"Unexpected exception while attempting to get context data. Context data is not supported for this logging framework. Exception: {e}");
                _contextDataNotSupported = true;
                return null;
            }
        }

        private AfterWrappedMethodDelegate DecorateLogMessage(MEL.ILogger logger, IAgent agent)
        {
            if (!agent.Configuration.LogDecoratorEnabled)
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
