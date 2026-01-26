// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Api.Agent;

/// <summary>
/// Provides access to Distributed Trace payload.
/// </summary>
public interface IDistributedTracePayload
{
    /// <summary>
    /// Returns a serialized, Base64-encoded version of the Distributed Trace payload.
    /// </summary>
    /// <example>
    /// <code>
    ///   KeyValuePair&lt;string, string&gt; metadata;
    ///   IAgent agent = GetAgent();
    ///   ITransaction transaction = agent.CurrentTransaction;
    ///   IDistributedTracePayload payload = transaction.CreateDistributedTracePayload();
    ///   metadata.Key = NewRelic.Api.Agent.Constants.DistributedTracePayloadKey;
    ///   metadata.Value = payload.HttpSafe();
    /// </code>
    /// </example>
    string HttpSafe();

    /// <summary>
    /// Returns a serialized, plain text version of the Distributed Trace payload.
    /// </summary>
    /// <example>
    /// <code>
    ///   KeyValuePair&lt;string, string&gt; metadata;
    ///   IAgent agent = GetAgent();
    ///   ITransaction transaction = agent.CurrentTransaction;
    ///   IDistributedTracePayload payload = transaction.CreateDistributedTracePayload();
    ///   metadata.Key = NewRelic.Api.Agent.Constants.DistributedTracePayloadKey;
    ///   metadata.Value = payload.Text();
    /// </code>
    /// </example>
    string Text();

    /// <summary>
    /// Returns true if the Distributed Trace payload is empty, false if it is not. This method is provided as a convenience method and to emphasize that it is possible the Agent will create an empty payload.
    /// </summary>
    /// <example>
    /// <code>
    ///   KeyValuePair&lt;string, string&gt; metadata;
    ///   IAgent agent = GetAgent();
    ///   ITransaction transaction = agent.CurrentTransaction;
    ///   IDistributedTracePayload payload = transaction.CreateDistributedTracePayload();
    ///   if (!payload.IsEmpty())
    ///   {
    ///      metadata.Key = NewRelic.Api.Agent.Constants.DistributedTracePayloadKey;
    ///      metadata.Value = payload.HttpSafe();
    ///   }
    /// </code>
    /// </example>
    bool IsEmpty();
}