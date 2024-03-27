// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// This interface contains methods we may eventually move to <see cref="Agent"/> once they have been sufficiently vetted.
    /// Methods on this interface are subject to refactoring or removal in future versions of the API.
    /// </summary>
    public interface IAgentExperimental
    {
        /// <summary>
        /// Records a supportability metric
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="count">Defaults to 1.0</param>
        void RecordSupportabilityMetric(string metricName, long count = 1);

        /// <summary>
        /// Records a count metric with the given name
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="count"></param>
        void RecordCountMetric(string metricName, long count = 1);
        /// <summary>
        /// Records a byte count metric with the given name
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="totalBytes"></param>
        /// <param name="exclusiveBytes"></param>
        void RecordByteMetric(string metricName, long totalBytes, long? exclusiveBytes = null);

        /// <summary>
        /// Records the log message in the transaction to later be forwarded if log forwarding is enabled.
        /// </summary>
        /// <param name="frameworkName">The name of the logging framework.</param>
        /// <param name="logEvent">The logging event object.</param>
        /// <param name="getTimestamp">A Func<object,DateTime> that knows how to get the timestamp from the logEvent.</param>
        /// <param name="getLogLevel">A Func<object,object> that knows how to get the log level from the logEvent.</param>
        /// <param name="getLogMessage">A Func<object,string> that knows how to get the log message from the logEvent</param>
        /// <param name="getLogException">A Func<object,Exception> that knows how to get the log exception from the logEvent</param>
        /// <param name="spanId">The span ID of the segment the log message occured within.</param>
        /// <param name="traceId">The trace ID of the transaction the log message occured within.</param>
        void RecordLogMessage(string frameworkName, object logEvent, Func<object,DateTime> getTimestamp, Func<object,object> getLogLevel, Func<object,string> getLogMessage, Func<object, Exception> getLogException, Func<object, Dictionary<string, object>> getContextData, string spanId, string traceId);

        Extensions.Helpers.IStackExchangeRedisCache StackExchangeRedisCache { get; set; }

        ISimpleSchedulingService SimpleSchedulingService { get; }

        void RecordLlmEvent(string eventType, IDictionary<string, object> attributes);
    }
}
