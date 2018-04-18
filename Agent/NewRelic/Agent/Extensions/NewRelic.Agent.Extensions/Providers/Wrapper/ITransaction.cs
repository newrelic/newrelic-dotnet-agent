using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.Extensions.Parsing;

namespace NewRelic.Agent.Extensions.Providers.Wrapper
{
	public interface ITransaction : IDisposable
	{
		/// <summary>
		/// Flags the current transaction as ignored.  The transaction will continue to be built up as normal, but once it is complete it will be dropped rather than being reported to New Relic.  No new segments will be created.
		/// </summary>
		void Ignore();

		/// <summary>
		/// Returns true if this transaction is valid.
		/// </summary>
		bool IsValid { get; }

		/// <summary>
		/// End this transaction.
		/// </summary>
		void End();

		/// <summary>
		/// Creates a segment for a datastore operation.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="commandText">The text representation of the operation being performed.  Not required, though when provided it's used for generating traces.</param>
		/// <exception cref="System.ArgumentNullException">Is thrown if <paramref name="operation"/> is null.</exception>
		/// <returns>An opaque object that will be needed when you want to end the segment.</returns>
		ISegment StartDatastoreSegment(MethodCall methodCall, ParsedSqlStatement parsedSqlStatement, [CanBeNull] ConnectionInfo connectionInfo = null, [CanBeNull] String commandText = null);

		/// <summary>
		/// Creates a segment for an external request operation.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="destinationUri">The destination URI of the external request</param>
		/// <param name="method">The method of the request, such as an HTTP verb (e.g. GET or POST)</param>
		/// <exception cref="System.ArgumentNullException">Is thrown if <paramref name="destinationUri"/> or <paramref name="method"/> is null.</exception>
		/// <returns>An opaque object that will be needed when you want to end the segment.</returns>
		ISegment StartExternalRequestSegment(MethodCall methodCall, [NotNull] Uri destinationUri, [NotNull] String method);



		/// <summary>
		/// Creates a segment for a method call.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="typeName">The name of the type. Must not be null.</param>
		/// <param name="methodName">The name of the method. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		/// <returns>an opaque object that will be needed when you want to end the segment.</returns>
		ISegment StartMethodSegment(MethodCall methodCall, [NotNull] String typeName, [CanBeNull] String methodName);

		/// <summary>
		/// Creates a segment with the &apos;Custom&apos; prefix for a method call.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="segmentName">The name of the custom segment. Must not be null and will be truncated to 255 characters.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		/// <returns>an opaque object that will be needed when you want to end the segment.</returns>
		ISegment StartCustomSegment(MethodCall methodCall, [NotNull] String segmentName);

		/// <summary>
		/// Creates a segment for sending to or receiving from a message brokering system.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="destinationType"></param>
		/// <param name="operation"></param>
		/// <param name="brokerVendorName">Must not be null.</param>
		/// <param name="destinationName">Can be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		/// <returns>an opaque object that will be needed when you want to end the segment.</returns>
		ISegment StartMessageBrokerSegment(MethodCall methodCall, MessageBrokerDestinationType destinationType, MessageBrokerAction operation, [NotNull] String brokerVendorName, [CanBeNull] String destinationName = null);

		/// <summary>
		/// Starts a transaction segment. Does nothing if there is no current transaction.
		/// </summary>
		/// <param name="methodCall">The method call that is responsible for starting this segment.</param>
		/// <param name="segmentName">The name of the segment that will be created. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		/// <returns>an opaque object that will be needed if you want to end the segment.</returns>
		ISegment StartTransactionSegment(MethodCall methodCall, [NotNull] String segmentName);

		/// <summary>
		/// Returns metadata that the agent wants to be attached to outbound requests.
		/// </summary>
		/// <returns></returns>
		[NotNull]
		IEnumerable<KeyValuePair<String, String>> GetRequestMetadata();
		
		/// <summary>
		/// Returns metadata that the agent wants to be attached to outbound responses.
		/// </summary>
		/// <returns>Collection of key value pairs representing the response metadata</returns>
		IEnumerable<KeyValuePair<String, String>> GetResponseMetadata();

		/// <summary>
		/// Tell the agent about an error that just occurred in the instrumented application.  If there is a transaction running the transaction will be flagged as an error transaction.
		/// </summary>
		/// <param name="exception">The exception associated with this error. Must not be null.</param>
		void NoticeError([NotNull] Exception exception);

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
		/// <exception cref="System.ArgumentNullException"></exception>
		void ProcessInboundResponse([NotNull] IEnumerable<KeyValuePair<string, string>> headers, [CanBeNull] ISegment segment);

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
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetWebTransactionName(WebTransactionType type, [NotNull] string name, int priority = 1);

		/// <summary>
		/// Sets the name of the current transaction to a name in the WebTransaction namespace which is derived from a path which will be normalized by the agent. Does nothing if there is no current transaction.
		/// </summary>
		/// <param name="type">The type of web transaction.</param>
		/// <param name="path">The path to use as the name of the transaction. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetWebTransactionNameFromPath(WebTransactionType type, [NotNull] string path);

		/// <summary>
		/// Sets the name of the current transaction to a name in the OtherTransaction namespace which is derived from some message broker details. Does nothing if there is no current transaction.
		/// </summary>
		/// <param name="destinationType"></param>
		/// <param name="brokerVendorName">The name of the message broker vendor. Must not be null.</param>
		/// <param name="destination">The destination queue of the message being handled. Can be null.</param>
		/// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetMessageBrokerTransactionName(MessageBrokerDestinationType destinationType, [NotNull] string brokerVendorName, [CanBeNull] string destination = null, int priority = 1);

		/// <summary>
		/// Sets the name of the current transaction to a custom name in the OtherTransaction namespace.  Does nothing if there is no current transaction.
		/// </summary>
		/// <param name="category">The general category of the transaction. Must not be null.</param>
		/// <param name="name">The name of the transaction. Must not be null.</param>
		/// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetOtherTransactionName([NotNull] string category, [NotNull] string name, int priority = 1);

		/// <summary>
		/// Sets the name of the current transaction to a custom transaction name. The namespace (WebTransaction or OtherTransaction) will be determined based on the current best transaction name's category. Does nothing if there is no current transaction.
		/// </summary>
		/// <param name="name">The name of the transaction. Must not be null.</param>
		/// <param name="priority">The priority of the name being set. Higher priority names override lower priority names.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetCustomTransactionName([NotNull] string name, int priority = 1);

		/// <summary>
		/// Set the URI for the current transaction (if there is one).
		/// </summary>
		/// <param name="uri">The URI for this transaction. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetUri([NotNull] string uri);

		/// <summary>
		/// Set the Original URL for the current transaction (if there is one).
		/// </summary>
		/// <param name="uri">The original URL for this transaction. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetOriginalUri([NotNull] string uri);

		/// <summary>
		/// Set the referrer URL for the current transaction (if there is one).
		/// </summary>
		/// <param name="uri">The referrer URL for this transaction. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetReferrerUri([NotNull] string uri);

		/// <summary>
		/// Set the queue time for the current transaction (if there is one).
		/// </summary>
		/// <param name="queueTime">The queue time for this transaction.</param>
		void SetQueueTime(TimeSpan queueTime);

		/// <summary>
		/// Set the request parameters for the current transaction (if there is one). If duplicate keys are used, or if this method is called twice with the same keys, it is undefined whether or not the new values will override the old values for those duplicate keys.
		/// </summary>
		/// <param name="parameters">The request parameters for this transaction. Must not be null.</param>
		/// <exception cref="System.ArgumentNullException"></exception>
		void SetRequestParameters([NotNull] IEnumerable<KeyValuePair<string, string>> parameters);

		/// <summary>
		/// Saves and returns the value of evaluating func to a cache. func is only evaluated if the key does not yet exist in the cache.
		/// </summary>
		/// <param name="key"></param>
		/// <param name="func"></param>
		/// <returns></returns>
		object GetOrSetValueFromCache(string key, Func<object> func);

		ParsedSqlStatement GetParsedDatabaseStatement(DatastoreVendor datastoreVendor, CommandType commandType, string sql);
	}
}
