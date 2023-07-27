// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Reflection;

namespace NewRelic.Providers.Wrapper.Logging
{
    public class Log4netWrapper : IWrapper
    {
        private static Func<object, object> _getLevel;
        private static Func<object, string> _getRenderedMessage;
        private static Func<object, DateTime> _getTimestamp;
        private static Func<object, Exception> _getLogException;
        private static Func<object, IDictionary> _getGetProperties; // calls GetProperties method
        private static Func<object, IDictionary> _getProperties; // getter for Properties property

        private static Func<object, object> _getLegacyProperties; // getter for legacy Properties property
        private static Func<object, Hashtable> _getLegacyHashtable; // getter for Properties hashtable property

        private static bool _legacyVersion = false;

        public bool IsTransactionRequired => false;


        private const string WrapperName = "log4net";

        public CanWrapResponse CanWrap(InstrumentedMethodInfo methodInfo)
        {
            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.Log4Net])
            {
                return new CanWrapResponse(WrapperName.Equals(methodInfo.RequestedWrapperName));
            }

            return new CanWrapResponse(false);
        }

        public AfterWrappedMethodDelegate BeforeWrappedMethod(InstrumentedMethodCall instrumentedMethodCall, IAgent agent, ITransaction transaction)
        {
            var logEvent = instrumentedMethodCall.MethodCall.MethodArguments[0];
            var logEventType = logEvent.GetType();

            RecordLogMessage(logEvent, logEventType, agent);

            DecorateLogMessage(logEvent, logEventType, agent);

            return Delegates.NoOp;
        }

        private void RecordLogMessage(object logEvent, Type logEventType, IAgent agent)
        {
            var getLevelFunc = _getLevel ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEventType, "Level");

            // RenderedMessage is get only
            var getRenderedMessageFunc = _getRenderedMessage ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<string>(logEventType, "RenderedMessage");

            // Older versions of log4net only allow access to a timestamp in local time
            var getTimestampFunc = _getTimestamp ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<DateTime>(logEventType, "TimeStamp");

            Func<object, Exception> getLogExceptionFunc;

            try
            {
                getLogExceptionFunc = _getLogException ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<Exception>(logEventType, "ExceptionObject");
            }
            catch
            {
                try
                {
                    // Legacy property, mainly used by Sitecore
                    getLogExceptionFunc = _getLogException ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<Exception>(logEventType, "m_thrownException");
                    _legacyVersion = true;
                }
                catch
                {
                    _getLogException = (x) => null;
                    getLogExceptionFunc = _getLogException;
                }

            }

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

            var getProperties = _getProperties ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary>(logEventType, "Properties");
            var propertiesDictionary = getProperties(logEvent);

            if (propertiesDictionary == null)
            {
                return;
            }

            // uses the foratted metadata to make a single entry
            var formattedMetadata = LoggingHelpers.GetFormattedLinkingMetadata(agent);

            // uses underscores to support other frameworks that do not allow hyphens (Serilog)
            propertiesDictionary["NR_LINKING"] = formattedMetadata;
        }

        private Dictionary<string, object> GetContextData(object logEvent)
        {
            var logEventType = logEvent.GetType();
            Func<object, IDictionary> getProperties;

            try
            {
                getProperties = _getGetProperties ??= VisibilityBypasser.Instance.GenerateParameterlessMethodCaller<IDictionary>(logEventType.Assembly.ToString(), logEventType.FullName, "GetProperties");
            }
            catch
            {
                try
                {
                    _legacyVersion = true;
                    // Legacy property, mainly used by Sitecore
                    getProperties = _getProperties ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<IDictionary>(logEventType, "MappedContext");
                }
                catch
                {
                    _getProperties = (x) => null;
                    getProperties = _getProperties;
                }
            }

            var contextData = new Dictionary<string, object>();
            // In older versions of log4net, there may be additional properties
            if (_legacyVersion)
            {
                // Properties is a "PropertiesCollection", an internal type
                var getLegacyProperties = _getLegacyProperties ??= VisibilityBypasser.Instance.GeneratePropertyAccessor<object>(logEventType, "Properties");
                var legacyProperties = getLegacyProperties(logEvent);

                // PropertyCollection has an internal hashtable that stores the data. The only public method for
                // retrieving the data is the indexer [] which is more of a pain to get via reflection.
                var propertyCollectionType = legacyProperties.GetType();
                var getHashtable = _getLegacyHashtable ??= VisibilityBypasser.Instance.GenerateFieldReadAccessor<Hashtable>(propertyCollectionType.Assembly.ToString(), propertyCollectionType.FullName, "m_ht");

                var hashtable = getHashtable(legacyProperties);

                foreach (var key in hashtable.Keys)
                {
                    contextData.Add(key.ToString(), hashtable[key]);
                }
            }

            var propertiesDictionary = getProperties(logEvent);

            if (propertiesDictionary != null && propertiesDictionary.Count > 0)
            {
                foreach (var key in propertiesDictionary.Keys)
                {
                    contextData.Add(key.ToString(), propertiesDictionary[key]);
                }
            }

            return contextData.Any() ? contextData : null;
        }
    }
}
