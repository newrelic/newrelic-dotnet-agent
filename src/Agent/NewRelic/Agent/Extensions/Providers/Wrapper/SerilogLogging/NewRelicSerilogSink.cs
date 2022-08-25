// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
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

            // This will either add the log message to the transaction or directly to the aggregator

            var xapi = _agent.GetExperimentalApi();
            xapi.RecordLogMessage("serilog", logEvent, getDateTimeFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, _agent.TraceMetadata.SpanId, _agent.TraceMetadata.TraceId);
        }
    }
}
