// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Api.Experimental
{
    /// <summary>
    /// This interface contains methods we may eventually move to <see cref="Agent"/> once they have been sufficiently vetted.
    /// Methods on this interface are subject to refactoring or removal in future versions of the API.
    /// </summary>
    public interface IAgentExperimental
    {
        /// <summary>
        /// Records a supportability metrics
        /// </summary>
        /// <param name="metricName"></param>
        /// <param name="count">Defaults to 1.0f</param>
        void RecordSupportabilityMetric(string metricName, int count = 1);

        /// <summary>
        /// Records the log message in the transaction to later be forwarded if log forwarding is enabled.
        /// </summary>
        /// <param name="timestamp">Timestamp from the log message.</param>
        /// <param name="logLevel">Severity or level of the log message.</param>
        /// <param name="logMessage">The log message.</param>
        /// <param name="spanId">The span ID of the segment the log message occured within.</param>
        /// <param name="traceId">The trace ID of the transaction the log message occured within.</param>
        /// <param name="frameworkName">The name of the logging framework</param>
        void RecordLogMessage(string frameworkName, DateTime timestamp, string logLevel, string logMessage, string spanId, string traceId);
    }
}
