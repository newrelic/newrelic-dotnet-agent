// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using NewRelic.Agent.Extensions.Logging;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Providers.Wrapper.SerilogLogging
{
    public class NewRelicSerilogSink : ILogEventSink
    {
        readonly IAgent _agent;

        public NewRelicSerilogSink(IAgent agent)
        {
            _agent = agent;
        }

        public void Emit(LogEvent logEvent)
        {
            //This check is to prevent forwarding duplicate logs when Microsoft.Extensions.Logging is used.
            if (!LogProviders.RegisteredLogProvider[(int)LogProvider.Serilog])
            {
                RecordLogMessage(logEvent);
            }
        }

        private void RecordLogMessage(LogEvent logEvent)
        {
            Func<object, object> getLevelFunc = l => logEvent.Level;

            Func<object, DateTime> getDateTimeFunc = l => logEvent.Timestamp.UtcDateTime;

            Func<object, Exception> getLogExceptionFunc = l => logEvent.Exception;

            Func<object, string> getMessageFunc = l => logEvent.RenderMessage();

            // Placeholder until context data (custom attribute) instrumentation is implemented
            Func<object, Dictionary<string, object>> getContextDataFunc = (logEvent) => null;

            var xapi = _agent.GetExperimentalApi();
            xapi.RecordLogMessage("serilog", logEvent, getDateTimeFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, _agent.TraceMetadata.SpanId, _agent.TraceMetadata.TraceId);
        }
    }
}
