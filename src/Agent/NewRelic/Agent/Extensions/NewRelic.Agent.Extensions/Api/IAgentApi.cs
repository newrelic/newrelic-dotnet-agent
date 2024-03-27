// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Api
{
    public interface IAgentApi
    {
        void InitializePublicAgent(object publicAgent);

        /// <summary> Record a custom analytics event represented by a name and a list of key-value pairs. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="eventType"/> or
        /// <paramref name="attributes"/> are null. </exception>
        ///
        /// <param name="eventType">  The name of the event to record. Only the first 255 characters (256
        /// including the null terminator) are retained. </param>
        /// <param name="attributes"> The attributes to associate with this event. </param>
        void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes);

        /// <summary> Record a named metric with the given duration. </summary>
        ///
        /// <param name="name">  The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="value"> The number of seconds to associate with the named attribute. This can be
        /// negative, 0, or positive. </param>
        void RecordMetric(string name, float value);

        /// <summary> Record response time metric. </summary>
        ///
        /// <param name="name">   The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="millis"> The milliseconds duration of the response time. </param>
        void RecordResponseTimeMetric(string name, long millis);

        /// <summary> Increment the metric counter for the given name. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="name"/> is null. </exception>
        ///
        /// <param name="name"> The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        void IncrementCounter(string name);

        /// <summary> Notice an error identified by an exception report it to the New Relic service. If
        /// this method is called within a transaction, the exception will be reported with the
        /// transaction when it finishes. If it is invoked outside of a transaction, a traced error will
        /// be created and reported to the New Relic service. Only the exception/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception"/> is null. </exception>
        ///
        /// <param name="exception">	    The exception to be reported. Only part of the exception's
        /// information may be retained to prevent the report from being too large. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        void NoticeError(Exception exception, IDictionary<string, string>? customAttributes);

        /// <summary> Notice an error identified by an exception report it to the New Relic service. If
        /// this method is called within a transaction, the exception will be reported with the
        /// transaction when it finishes. If it is invoked outside of a transaction, a traced error will
        /// be created and reported to the New Relic service. Only the exception/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception"/> is null. </exception>
        ///
        /// <param name="exception">	    The exception to be reported. Only part of the exception's
        /// information may be retained to prevent the report from being too large. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        void NoticeError(Exception exception, IDictionary<string, object>? customAttributes);

        /// <summary> Notice an error identified by an exception report it to the New Relic service. If
        /// this method is called within a transaction, the exception will be reported with the
        /// transaction when it finishes. If it is invoked outside of a transaction, a traced error will
        /// be created and reported to the New Relic service. Only the exception/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception"/> is null. </exception>
        ///
        /// <param name="exception">	    The exception to be reported. Only part of the exception's
        /// information may be retained to prevent the report from being too large. </param>
        void NoticeError(Exception exception);

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">		    The message to be displayed in the traced error. 
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.</param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        void NoticeError(string message, IDictionary<string, string>? customAttributes);

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">		    The message to be displayed in the traced error. 
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.</param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        void NoticeError(string message, IDictionary<string, object>? customAttributes);

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">    The message to be displayed in the traced error. 
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.</param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        /// <param name="isExpected"> Marks the error expected.
        /// </param>
        void NoticeError(string message, IDictionary<string, string>? customAttributes, bool isExpected);

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">    The message to be displayed in the traced error. 
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.</param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        /// <param name="isExpected"> Marks the error expected.
        /// </param>
        void NoticeError(string message, IDictionary<string, object>? customAttributes, bool isExpected);

        /// <summary> Set the name of the current transaction. Supports web applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="key"/> is null. </exception>
        ///
        /// <param name="category"> The category of this transaction, used to distinguish different types
        /// of transactions. Only the first 1000 characters are retained.  If null is passed, the
        /// category defaults to "Custom". </param>
        /// <param name="name">	    The name of the transaction starting with a forward slash.  example:
        /// /store/order Only the first 1000 characters are retained. </param>
        void SetTransactionName(string? category, string name);

        /// <summary> Sets transaction URI. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="uri"/> is null. </exception>
        ///
        /// <param name="uri"> URI to be associated with the transaction. </param>
        void SetTransactionUri(Uri uri);

        /// <summary> Sets the User Name, Account Name and Product Name to associate with the RUM
        /// JavaScript footer for the current web transaction. Supports web applications only. </summary>
        ///
        /// <param name="userName">    Name of the user to be associated with the transaction. </param>
        /// <param name="accountName"> Name of the account to be associated with the transaction. </param>
        /// <param name="productName"> Name of the product to be associated with the transaction. </param>
        void SetUserParameters(string? userName, string? accountName, string? productName);

        /// <summary> Ignore the transaction that is currently in process. Supports web applications only. </summary>
        void IgnoreTransaction();

        /// <summary> Ignore the current transaction in the apdex computation. Supports web applications
        /// only. </summary>
        void IgnoreApdex();

        /// <summary> Returns the HTML snippet to be inserted into the header of HTML pages to enable Real
        /// User Monitoring. The HTML will instruct the browser to fetch a small JavaScript file and
        /// start the page timer. Supports web applications only. </summary>
        ///
        /// <returns> An HTML string to be embedded in a page header. </returns>
        ///
        /// <example> <code>
        /// &lt;html&gt;
        ///   &lt;head&gt;
        ///     &lt;&#37;= NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader()&#37;&gt;
        ///   &lt;/head&gt;
        ///   &lt;body&gt;
        ///   ...
        /// </code></example>
        string GetBrowserTimingHeader();

        /// <summary> Returns the HTML snippet to be inserted into the header of HTML pages to enable Real
        /// User Monitoring. The HTML will instruct the browser to fetch a small JavaScript file and
        /// start the page timer. Supports web applications only. </summary>
        /// <param name="nonce">Cryptographic nonce used by a <c>Content-Security-Policy</c> <c>script-src</c> policy.</param>
        ///
        /// <returns> An HTML string to be embedded in a page header. </returns>
        ///
        /// <example> <code>
        /// &lt;html&gt;
        ///   &lt;head&gt;
        ///     &lt;&#37;= NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader("random-nonce")&#37;&gt;
        ///   &lt;/head&gt;
        ///   &lt;body&gt;
        ///   ...
        /// </code></example>
        string GetBrowserTimingHeader(string nonce);

        /// <summary> Disables the automatic instrumentation of browser monitoring hooks in individual
        /// pages Supports web applications only. </summary>
        ///
        /// <param name="overrideManual"> (Optional) True to override manual. </param>
        ///
        /// <example><code>
        /// NewRelic.Api.Agent.NewRelic.DisableBrowserMonitoring()
        /// </code></example>
        void DisableBrowserMonitoring(bool overrideManual = false);

        /// <summary> (This API should only be used from the public API)  Starts the agent (i.e. begin
        /// capturing data). Does nothing if the agent is already started. Useful if agent autostart is
        /// disabled via configuration, or if you want to ensure the agent is started before using other
        /// methods in the Agent API. </summary>
        ///
        /// <example><code>
        ///   NewRelic.Api.Agent.NewRelic.StartAgent();
        /// </code></example>
        void StartAgent();

        /// <summary> Sets the name of the application to <paramref name="applicationName"/>. At least one
        /// given name must not be null.
        /// 
        /// An application may also have up to two additional names. This can be useful, for example, to
        /// have multiple applications report under the same roll-up name. </summary>
        ///
        /// <exception cref="ArgumentException"> Thrown when <paramref name="applicationName"/>,
        /// <paramref name="applicationName2"/> and <paramref name="applicationName3"/> are all null. </exception>
        ///
        /// <param name="applicationName">  The main application name. </param>
        /// <param name="applicationName2"> (Optional) The second application name. </param>
        /// <param name="applicationName3"> (Optional) The third application name. </param>
        void SetApplicationName(string applicationName, string? applicationName2 = null, string? applicationName3 = null);

        /// <summary> Gets the request metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        IEnumerable<KeyValuePair<string, string>>? GetRequestMetadata();

        /// <summary> Gets the response metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        IEnumerable<KeyValuePair<string, string>> GetResponseMetadata();

        /// <summary> Sets the method that will be invoked to define the error group that an exception
        /// should belong to.
        ///
        /// The callback takes an IReadOnlyDictionary of attributes, the stack trace, and Exception,
        /// and returns the name of the error group to use. Return values
        /// that are null, empty, or whitespace will not associate the Exception to an error group.
        /// </summary>
        /// <param name="callback">The callback to invoke to define the error group that an Exception belongs to.</param>
        void SetErrorGroupCallback(Func<IReadOnlyDictionary<string, object>, string> callback);

        /// <summary> Sets the method that will be invoked to define the token count on completion.
        ///
        /// The callback takes the model name and input value, and returns an integer of the token count.
        /// A value returned from the callback that is less than or equal to 0 will be ignored.
        /// </summary>
        /// <param name="callback">The callback to invoke to generate the token count based on the model and input..</param>
        void SetLlmTokenCountingCallback(Func<string, string, int> callback);

        /// <summary>
        /// Creates an event with the customer feedback on the LLM interaction.
        /// </summary>
        /// <param name="traceId">Required. ID of the trace where the chat completion(s) related to the feedback occurred</param>
        /// <param name="rating">Required. Rating provided by an end user. Must be string or int</param>
        /// <param name="category">Optional. Category of the feedback as provided by the end user</param>
        /// <param name="message">Optional. Freeform text feedback from an end user</param>
        /// <param name="metadata">Optional. Set of key-value pairs to store any other desired data to submit with the feedback event</param>
        void RecordLlmFeedbackEvent(string traceId, object rating, string category = "", string message = "", IDictionary<string, object>? metadata = null);
    }
}

#nullable restore
