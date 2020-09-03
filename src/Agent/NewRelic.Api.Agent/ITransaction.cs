// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Api.Agent
{
    /// <summary>
    /// Provides access to transaction-specific methods in the New Relic API.
    /// </summary>
    public interface ITransaction
    {
        /// <summary>
        /// Accepts an incoming Distributed Trace payload from another service.
        /// </summary>
        /// <param name="payload">A string representation of the incoming Distributed Trace payload.</param>
        /// <param name="transportType">An enum value describing the transport of the incoming payload (e.g. http). Default is TransportType.Unknown.</param>
        /// <example>
        /// <code>
        ///   KeyValuePair&lt;string, string&gt; metadata;
        ///   IAgent agent = GetAgent();
        ///   ITransaction transaction = agent.CurrentTransaction;
        ///   transaction.AcceptDistributedTracePayload(metadata.Value, TransportType.Queue);
        /// </code>
        /// </example>
        [Obsolete("AcceptDistributedTracePayload is deprecated.")]
        void AcceptDistributedTracePayload(string payload, TransportType transportType = TransportType.Unknown);

        /// <summary>
        /// Creates a Distributed Trace payload for inclusion in an outgoing request.
        /// </summary>
        /// <example>
        /// <code>
        ///   IAgent agent = GetAgent();
        ///   ITransaction transaction = agent.CurrentTransaction;
        ///   IDistributedTracePayload payload = transaction.CreateDistributedTracePayload();
        /// </code>
        /// </example>
        /// <returns>Returns an object providing access to the outgoing payload.</returns>
        [Obsolete("CreateDistributedTracePayload is deprecated.")]
        IDistributedTracePayload CreateDistributedTracePayload();

        /// <summary>
        /// Accept incoming Trace Context headers from another service.
        /// </summary>
        /// <typeparam name="T">Data type of the carrier</typeparam>
        /// <param name="carrier">Source of incoming Trace Context headers.</param>
        /// <param name="getter">Caller-defined function to extract header data from the carrier.</param>
        /// <param name="transportType">An enum value describing the transport of the incoming payload (e.g. http). Default is TransportType.Unknown.</param>
        /// <example>
        /// <code>
        ///   HttpContext httpContext = HttpContext.Current;
        ///   IAgent agent = GetAgent();
        ///   ITransaction currentTransaction = _agent.CurrentTransaction;
        ///   currentTransaction.AcceptDistributedTraceHeaders(httpContext, Getter, TransportType.HTTP);
        ///   
        ///   IEnumerable&gt;string&lt; Getter(HttpContext carrier, string key)
        ///   {
        ///      string value = carrier.Request.Headers[key];
        ///      return value == null ? null : new string[] { value };
        ///   }
        /// </code>
        /// </example>
        void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType);

        /// <summary>
        /// Insert outgoing Trace Context headers in an outgoing request. 
        /// </summary>
        /// <typeparam name="T">Data type of the carrier</typeparam>
        /// <param name="carrier">Container where Trace Context headers are inserted.</param>
        /// <param name="setter">Caller-defined Action to insert header data into the carrier.</param>
        /// <example>
        /// <code>
        ///   HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, "https://remote-address");
        ///   IAgent agent = GetAgent();
        ///   ITransaction currentTransaction = _agent.CurrentTransaction;
        ///
        ///   var setter = new Action&gt;System.Net.HttpWebRequest, string, string&lt;((carrier, key, value) =>
        ///   {
        ///	     carrier.Headers?.Set(key, value);
        ///   });
        ///
        ///   currentTransaction.InsertDistributedTraceHeaders(requestMessage, setter);
        /// </code>
        /// </example>
        void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter);

        /// <summary> Add a key/value pair to the transaction.  These are reported in errors and
        /// transaction traces.</summary>
        ///
        /// <param name="key">   The key name to add to the transaction attributes. Limited to 255-bytes.</param>
        /// <param name="value"> The value to add to the current transaction.  Values are limited to 255-bytes.</param>
        ITransaction AddCustomAttribute(string key, object value);

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
    }
}
