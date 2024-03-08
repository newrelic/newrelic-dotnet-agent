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

        /// <summary>
        /// Sets a User Id to be associated with this transaction.
        /// </summary>
        /// <param name="userid">The User Id for this transaction.</param>
        void SetUserId(string userid);

        /// <summary>
        /// Records a datastore segment.
        /// This function allows an unsupported datastore to be instrumented in the same way as the .NET agent automatically instruments its supported datastores.
        /// </summary>
        /// <param name="vendor">Datastore vendor name, for example MySQL, MSSQL, MongoDB.</param>
        /// <param name="model">Table name or similar in non-relational datastores.</param>
        /// <param name="operation">Operation being performed, for example "SELECT" or "UPDATE" for SQL databases.</param>
        /// <param name="commandText">Optional. Query or similar in non-relational datastores.</param>
        /// <param name="host">Optional. Server hosting the datastore</param>
        /// <param name="portPathOrID">Optional. Port, path or other ID to aid in identifying the datastore.</param>
        /// <param name="databaseName">Optional. Datastore name.</param>
        /// <returns>IDisposable segment wrapper that both creates and ends the segment automatically.</returns>
        SegmentWrapper? RecordDatastoreSegment(string vendor, string model, string operation,
            string? commandText = null, string? host = null, string? portPathOrID = null, string? databaseName = null);
    }
}
