// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
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
            RecordLogMessage(logEvent);
        }

        private void RecordLogMessage(LogEvent logEvent)
        {
            Func<object, object> getLevelFunc = l => logEvent.Level;

            Func<object, DateTime> getDateTimeFunc = l => logEvent.Timestamp.UtcDateTime;

            Func<object, Exception> getLogExceptionFunc = l => logEvent.Exception;

            Func<object, string> getMessageFunc = l => logEvent.RenderMessage();

            Func<object, Dictionary<string, object>> getContextDataFunc = (o) =>
            {
                Dictionary<string, object> context = new Dictionary<string, object>();
                var properties = logEvent.Properties;
                if ((properties == null) || (properties.Count == 0))
                {
                    return context;
                }
                foreach (var pair in properties)
                {
                    // We can keep simple types as they are, but complex types are stored in nested LogEventPropertyValue objects, which
                    // are complicated to serialize as JSON. For those, use Render() to at least format them nicely as a string
                    if (pair.Value is ScalarValue scalar)
                    {
                        context[pair.Key] = scalar.Value;
                    }
                    else
                    {
                        using (StringWriter sw = new StringWriter())
                        {
                            pair.Value.Render(sw);
                            context[pair.Key] = sw.ToString();
                        }
                    }
                }
                return context;
            };

            var xapi = _agent.GetExperimentalApi();
            xapi.RecordLogMessage("serilog", logEvent, getDateTimeFunc, getLevelFunc, getMessageFunc, getLogExceptionFunc, getContextDataFunc, _agent.TraceMetadata.SpanId, _agent.TraceMetadata.TraceId);
        }
    }
}
