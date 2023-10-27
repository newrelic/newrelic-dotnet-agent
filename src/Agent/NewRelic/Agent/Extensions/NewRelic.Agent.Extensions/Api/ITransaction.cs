// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Api
{
    public interface ITransaction
    {
        void LogFinest(string message);

        /// <summary>
        /// Flags the current transaction as ignored.  The transaction will continue to be built up as normal, but once it is complete it will be dropped rather than being reported to New Relic.  No new segments will be created.
        /// </summary>
        void Ignore();

        /// <summary>
        /// Returns true if this transaction is valid.
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// Returns true if this transaction's timer has been stopped
        /// </summary>
        bool IsFinished { get; }

        /// <summary>
        /// The current segment from the segment call stack.
        /// </summary>
        ISegment CurrentSegment { get; }

        /// <summary>
        /// End this transaction.
        /// </summary>
        void End(bool captureResponseTime = true);

        /// <summary>
        /// Creates a segment for a datastore operation.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="commandText">The text representation of the operation being performed.  Not required, though when provided it's used for generating traces.</param>
        /// <param name="isLeaf">If set to true, the created segment is a leaf segment. The default value is false.</param>
        /// <exception cref="ArgumentNullException">Is thrown if <paramref name="operation"/> is null.</exception>
        /// <returns>An opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, ConnectionInfo connectionInfo = null, string commandText = null, IDictionary<string, IConvertible> queryParameters = null, bool isLeaf = false);

        /// <summary>
        /// Creates a segment for an external request operation.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="destinationUri">The destination URI of the external request</param>
        /// <param name="method">The method of the request, such as an HTTP verb (e.g. GET or POST)</param>
        /// <param name="isLeaf">If set to true, the created segment is a leaf segment. The default value is false.</param>
        /// <exception cref="ArgumentNullException">Is thrown if <paramref name="destinationUri"/> or <paramref name="method"/> is null.</exception>
        /// <returns>An opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartExternalRequestSegment(MethodCall methodCall, Uri destinationUri, string method, bool isLeaf = false);

        /// <summary>
        /// Creates a segment for a method call.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="typeName">The name of the type. Must not be null.</param>
        /// <param name="methodName">The name of the method. Must not be null.</param>
        /// <param name="isLeaf">If true, no child segments will be created from this one. Defaults to false.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>an opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartMethodSegment(MethodCall methodCall, string typeName, string methodName, bool isLeaf = false);

        /// <summary>
        /// Creates a segment with the &apos;Custom&apos; prefix for a method call.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="segmentName">The name of the custom segment. Must not be null and will be truncated to 255 characters.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>an opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartCustomSegment(MethodCall methodCall, string segmentName);

        /// <summary>
        /// Creates a segment for sending to or receiving from a message brokering system.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="destinationType"></param>
        /// <param name="operation"></param>
        /// <param name="brokerVendorName">Must not be null.</param>
        /// <param name="destinationName">Can be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>an opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName = null);

        /// <summary>
        /// Creates a segment for serializing a key or value in a message brokering system..
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="destinationType"></param>
        /// <param name="operation"></param>
        /// <param name="brokerVendorName">Must not be null.</param>
        /// <param name="destinationName">Can be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>an opaque object that will be needed when you want to end the segment.</returns>
        ISegment StartMessageBrokerSerializationSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, string brokerVendorName, string destinationName, string kind);

        /// <summary>
        /// Starts a transaction segment. Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="methodCall">The method call that is responsible for starting this segment.</param>
        /// <param name="segmentName">The name of the segment that will be created. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        /// <returns>an opaque object that will be needed if you want to end the segment.</returns>
        ISegment StartTransactionSegment(MethodCall methodCall, string segmentName);

        /// <summary>
        /// Returns metadata that the agent wants to be attached to outbound requests.
        /// </summary>
        /// <returns></returns>
        IEnumerable<KeyValuePair<string, string>> GetRequestMetadata();

        /// <summary>
        /// Returns metadata that the agent wants to be attached to outbound responses.
        /// </summary>
        /// <returns>Collection of key value pairs representing the response metadata</returns>
        IEnumerable<KeyValuePair<string, string>> GetResponseMetadata();

        /// <summary>
        /// Returns the distributed trace payload model that is attached to the outbound request.
        /// </summary>
        /// <returns>The distributed trace payload model representing the outgoing request.</returns>
        IDistributedTracePayload CreateDistributedTracePayload();

        /// <summary>
        /// Tell the agent about an error that just occurred in the instrumented application.  If there is a transaction running the transaction will be flagged as an error transaction.
        /// </summary>
        /// <param name="exception">The exception associated with this error. Must not be null.</param>
        void NoticeError(Exception exception);

        /// <summary>
        /// Sets the HTTP Response status code on the transaction.
        /// </summary>
        /// <param name="statusCode">The status code to set the transaction to.</param>
        /// <param name="subStatusCode">The IIS sub-status code if available.  Null otherwise.</param>
        void SetHttpResponseStatusCode(int statusCode, int? subStatusCode = null);

        /// <summary>
        /// Attaches the transaction to async storage. Should only call this from async methods.
        /// </summary>
        void AttachToAsync();

        /// <summary>
        /// Detatches the transaction from each active context storage. This is necessary when the transaction may be stored in a context 
        /// that can continue to persist, such as thread static or thread local storgage.
        /// </summary>
        void Detach();

        /// <summary>
        /// Detatches the transaction from each non-async active context storage. This is necessary when async tracking needs to continue but
        /// the primary context(s) the transaction may be stored can continue to persist, such as thread static or thread local storgage.
        /// </summary>
        void DetachFromPrimary();

        /// <summary>
        /// Processes headers from an inbound response to an earlier request. Does nothing if there is no outstanding transaction or if <paramref name="segment"/> is null.
        /// </summary>
        /// <param name="headers">The headers to be processed. Must not be null.</param>
        /// <param name="segment">The segment that was created with the original outbound request.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void ProcessInboundResponse(IEnumerable<KeyValuePair<string, string>> headers, ISegment segment);

        /// <summary>
        /// Prevents transaction from ending
        /// </summary>
        void Hold();

        /// <summary>
        /// Allows transaction to end.
        /// </summary>
        void Release();

        /// <summary>
        /// Sets the name of the current transaction to a name in the WebTransaction namespace. Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="type">The type of web transaction.</param>
        /// <param name="name">The name of the transaction. Must not be null.</param>
        /// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetWebTransactionName(WebTransactionType type, string name, TransactionNamePriority priority = TransactionNamePriority.Uri);

        /// <summary>
        /// Sets the name of the current transaction to a name in the WebTransaction namespace which is derived from a path which will be normalized by the agent. Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="type">The type of web transaction.</param>
        /// <param name="path">The path to use as the name of the transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetWebTransactionNameFromPath(WebTransactionType type, string path);

        /// <summary>
        /// Sets the name of the current transaction to a name in the OtherTransaction namespace which is derived from some message broker details. Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="destinationType"></param>
        /// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
        /// <param name="destination">The destination queue of the message being handled. Can be null.</param>
        /// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri);

        /// <summary>
        /// Sets the name of the current transaction to a name in the OtherTransaction namespace which is derived from some message broker details,
        /// conforming to the naming requirements of the Kafka spec . Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="destinationType"></param>
        /// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
        /// <param name="destination">The destination queue of the message being handled. Can be null.</param>
        /// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetKafkaMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, string brokerVendorName, string destination = null, TransactionNamePriority priority = TransactionNamePriority.Uri);

        /// <summary>
        /// Sets the name of the current transaction to a custom name in the OtherTransaction namespace.  Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="category">The general category of the transaction. Must not be null.</param>
        /// <param name="name">The name of the transaction. Must not be null.</param>
        /// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetOtherTransactionName(string category, string name, TransactionNamePriority priority = TransactionNamePriority.Uri);

        /// <summary>
        /// Sets the name of the current transaction to a custom transaction name. The namespace (WebTransaction or OtherTransaction) will be determined based on the current best transaction name's category. Does nothing if there is no current transaction.
        /// </summary>
        /// <param name="name">The name of the transaction. Must not be null.</param>
        /// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetCustomTransactionName(string name, TransactionNamePriority priority = TransactionNamePriority.Uri);

        /// <summary>
        /// Set the Request Method for the current transaction (if there is one).
        /// </summary>
        /// <param name="requestMethod">The Request Method for this transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetRequestMethod(string requestMethod);

        /// <summary>
        /// Set the URI for the current transaction (if there is one).
        /// </summary>
        /// <param name="uri">The URI for this transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetUri(string uri);

        /// <summary>
        /// Set the Original URL for the current transaction (if there is one).
        /// </summary>
        /// <param name="uri">The original URL for this transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetOriginalUri(string uri);

        /// <summary>
        /// Set the referrer URL for the current transaction (if there is one).
        /// </summary>
        /// <param name="uri">The referrer URL for this transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetReferrerUri(string uri);

        /// <summary>
        /// Set the queue time for the current transaction (if there is one).
        /// </summary>
        /// <param name="queueTime">The queue time for this transaction.</param>
        void SetQueueTime(TimeSpan queueTime);

        /// <summary>
        /// Set the request parameters for the current transaction (if there is one). If duplicate keys are used, or if this method is called twice with the same keys, it is undefined whether or not the new values will override the old values for those duplicate keys.
        /// </summary>
        /// <param name="parameters">The request parameters for this transaction. Must not be null.</param>
        /// <exception cref="ArgumentNullException"></exception>
        void SetRequestParameters(IEnumerable<KeyValuePair<string, string>> parameters);

        /// <summary>
        /// Saves and returns the value of evaluating func to a cache. func is only evaluated if the key does not yet exist in the cache.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        object GetOrSetValueFromCache(string key, Func<object> func);

        ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql);

        ITransaction AddCustomAttribute(string key, object value);

        void InsertDistributedTraceHeaders<T>(T carrier, Action<T, string, string> setter);

        void AcceptDistributedTraceHeaders<T>(T carrier, Func<T, string, IEnumerable<string>> getter, TransportType transportType);

        ITransaction SetRequestHeaders<T>(T headers, IEnumerable<string> keysToCapture, Func<T, string, string> getter);

        /// <summary>
        /// Sets a User Id to be associated with this transaction.
        /// </summary>
        /// <param name="userid">The User Id for this transaction.</param>
        void SetUserId(string userid);
    }
}
