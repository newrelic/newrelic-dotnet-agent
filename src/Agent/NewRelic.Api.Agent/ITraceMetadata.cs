// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

/// <summary>
/// 
/// </summary>
public interface ITraceMetadata
{
    /// <summary>
    /// Returns the trace identifier of the currently executing transaction.
    /// An empty String will be returned if the transaction does not support this functionality, distributed
    /// tracing is disabled or if the trace identifier is not known at the time of the method call.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   var traceMetadata = agent.TraceMetadata;
    ///   Console.WriteLine($"TraceId: {traceMetadata.TraceId}.");
    /// </code>
    /// </example>
    string TraceId { get; }

    /// <summary>
    /// Returns the span identifier associated with the current executing span.
    /// An empty String will be returned if the span does not support this functionality, distributed
    /// tracing is disabled or if the span identifier is not known at the time of the method call.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   var traceMetadata = agent.TraceMetadata;
    ///   Console.WriteLine($"SpanId: {traceMetadata.SpanId}.");
    /// </code>
    /// </example>
    string SpanId { get; }

    /// <summary>
    /// Is the current transaction sampled from a distributed tracing perspective.
    /// Returns true if distributed tracing is enabled and this transaction is sampled, false otherwise.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   var traceMetadata = agent.TraceMetadata;
    ///   Console.WriteLine($"IsSampled: {traceMetadata.IsSampled}.");
    /// </code>
    /// </example>

    bool IsSampled { get; }
}