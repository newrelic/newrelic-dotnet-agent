// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Api.Agent;

/// <summary>
/// Provides access to Agent artifacts and methods, such as the currently executing transaction.
/// </summary>
public interface IAgent
{
    /// <summary>
    /// Property providing access to the currently executing transaction via the ITransaction interface.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   ITransaction transaction = agent.CurrentTransaction;
    /// </code>
    /// </example>
    ITransaction CurrentTransaction { get; }

    /// <summary>
    /// Property providing access to the currently executing span via the ISpan interface.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   ITransaction transaction = agent.CurrentTransaction;
    ///   ISpan span = transaction.CurrentSpan;
    /// </code>
    /// </example>
    ISpan CurrentSpan { get; }

    /// <summary>
    /// Provides access to Trace Metadata for details about the currently executing distributed trace.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   var traceMetadata = agent.TraceMetadata();
    /// </code>
    /// </example>
    ITraceMetadata TraceMetadata { get; }

    /// <summary>
    /// Returns a list of key/value pairs that can be used to correlate this application in the New Relic backend.
    /// </summary>
    /// <example>
    /// <code>
    ///   IAgent agent = GetAgent();
    ///   var linkingMetadata = agent.LinkingMetadata();
    /// </code>
    /// </example>
    Dictionary<string, string> GetLinkingMetadata();
}