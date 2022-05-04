// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Api;
using NewRelic.Agent.Api.Experimental;
using Serilog.Core;
using Serilog.Events;

namespace NewRelic.Providers.Wrapper.SerilogLogging
{
    public class CustomSink : ILogEventSink
    {
        IAgent _agent;

        public CustomSink(IAgent agent)
        {
            _agent = agent;
        }

        public void Emit(LogEvent logEvent)
        {
            RecordLogMessage(logEvent);
        }

        private void RecordLogMessage(LogEvent logEvent)
        {
            Func<object, object> getLogLevelFunc = a => logEvent.Level;

            Func<object, DateTime> getDateTimeFunc = a => logEvent.Timestamp.UtcDateTime;

            Func<object, string> getMessageFunc = a => logEvent.RenderMessage();

            // This will either add the log message to the transaction or directly to the aggregator

            var xapi = _agent.GetExperimentalApi();
            xapi.RecordLogMessage("serilog", logEvent, getDateTimeFunc, getLogLevelFunc, getMessageFunc, _agent.TraceMetadata.SpanId, _agent.TraceMetadata.TraceId);
        }
    }
}
