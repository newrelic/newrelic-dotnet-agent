// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Api
{
    public static class InternalApi
    {
        private static IAgentApi? _agentApiImplementation;

        public static void SetAgentApiImplementation(IAgentApi agentApiImplementation)
        {
            _agentApiImplementation = agentApiImplementation;
        }

        public static void InitializePublicAgent(object publicAgent)
        {
            _agentApiImplementation?.InitializePublicAgent(publicAgent);
        }

        /// <summary> Record a custom analytics event represented by a name and a list of key-value pairs. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="eventType"/> or
        /// <paramref name="attributes"/> are null. </exception>
        ///
        /// <param name="eventType">  The name of the event to record. Only the first 255 characters (256
        /// including the null terminator) are retained. </param>
        /// <param name="attributes"> The attributes to associate with this event. </param>
        public static void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            _agentApiImplementation?.RecordCustomEvent(eventType, attributes);
        }

        /// <summary> Record a named metric with the given duration. </summary>
        ///
        /// <param name="name">  The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="value"> The number of seconds to associate with the named attribute. This can be
        /// negative, 0, or positive. </param>
        public static void RecordMetric(string name, float value)
        {
            _agentApiImplementation?.RecordMetric(name, value);
        }

        /// <summary> Record response time metric. </summary>
        ///
        /// <param name="name">   The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="millis"> The milliseconds duration of the response time. </param>
        public static void RecordResponseTimeMetric(string name, long millis)
        {
            _agentApiImplementation?.RecordResponseTimeMetric(name, millis);
        }

        /// <summary> Increment the metric counter for the given name. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="name"/> is null. </exception>
        ///
        /// <param name="name"> The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        public static void IncrementCounter(string name)
        {
            _agentApiImplementation?.IncrementCounter(name);
        }

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
        public static void NoticeError(Exception exception, IDictionary<string, string>? customAttributes)
        {
            _agentApiImplementation?.NoticeError(exception, customAttributes);
        }

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
        public static void NoticeError(Exception exception, IDictionary<string, object>? customAttributes)
        {
            _agentApiImplementation?.NoticeError(exception, customAttributes);
        }

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
        public static void NoticeError(Exception exception)
        {
            _agentApiImplementation?.NoticeError(exception);
        }

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">		    The message to be displayed in the traced error. Only the
        /// first 1000 characters are retained. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        public static void NoticeError(string message, IDictionary<string, string>? customAttributes)
        {
            _agentApiImplementation?.NoticeError(message, customAttributes);
        }

        public static void NoticeError(string message, IDictionary<string, string>? customAttributes, bool isExpected)
        {
            _agentApiImplementation?.NoticeError(message, customAttributes, isExpected);
        }

        /// <summary> Notice an error identified by a simple message and report it to the New Relic
        /// service. If this method is called within a transaction, the exception will be reported with
        /// the transaction when it finishes. If it is invoked outside of a transaction, a traced error
        /// will be created and reported to the New Relic service. Only the string/parameter pair for the
        /// first call to NoticeError during the course of a transaction is retained. Supports web
        /// applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        ///
        /// <param name="message">		    The message to be displayed in the traced error. Only the
        /// first 1000 characters are retained. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        public static void NoticeError(string message, IDictionary<string, object>? customAttributes)
        {
            _agentApiImplementation?.NoticeError(message, customAttributes);
        }

        public static void NoticeError(string message, IDictionary<string, object>? customAttributes, bool isExpected)
        {
            _agentApiImplementation?.NoticeError(message, customAttributes, isExpected);
        }

        /// <summary> Add a key/value pair to the current transaction.  These are reported in errors and
        /// transaction traces. Supports web applications only. </summary>
        ///
        /// <param name="key">   The key name to add to the transaction parameters. Only the first 1000
        /// characters are retained. </param>
        /// <param name="value"> The numeric value to add to the current transaction. If the value is a
        /// float it is recorded as a number, otherwise, <paramref name="value"/> is converted to a
        /// string. (via <c>value.ToString(CultureInfo.InvariantCulture);</c> </param>
        [Obsolete("Will be dropped in a future version.  Use Transaction.AddCustomAttribute instead")]
        public static void AddCustomParameter(string key, IConvertible value)
        {
            _agentApiImplementation?.AddCustomParameter(key, value);
        }

        /// <summary> A Add a key/value pair to the current transaction.  These are reported in errors and
        /// transaction traces. Supports web applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="key"/> or
        /// <paramref name="value"/> is null. </exception>
        ///
        /// <param name="key">   The key name for the custom parameter.  Only the first 1000 characters
        /// are retained. </param>
        /// <param name="value"> The value associated with the custom parameter. Only the first 1000
        /// characters are retained. </param>
        [Obsolete("Will be dropped in a future version.  Use Transaction.AddCustomAttribute instead")]
        public static void AddCustomParameter(string key, string value)
        {
            _agentApiImplementation?.AddCustomParameter(key, value);
        }

        /// <summary> Set the name of the current transaction. Supports web applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="key"/> is null. </exception>
        ///
        /// <param name="category"> The category of this transaction, used to distinguish different types
        /// of transactions. Only the first 1000 characters are retained.  If null is passed, the
        /// category defaults to "Custom". </param>
        /// <param name="name">	    The name of the transaction starting with a forward slash.  example:
        /// /store/order Only the first 1000 characters are retained. </param>
        public static void SetTransactionName(string? category, string name)
        {
            _agentApiImplementation?.SetTransactionName(category, name);
        }

        /// <summary> Sets transaction URI. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="uri"/> is null. </exception>
        ///
        /// <param name="uri"> URI to be associated with the transaction. </param>
        public static void SetTransactionUri(Uri uri)
        {
            _agentApiImplementation?.SetTransactionUri(uri);
        }

        /// <summary> Sets the User Name, Account Name and Product Name to associate with the RUM
        /// JavaScript footer for the current web transaction. Supports web applications only. </summary>
        ///
        /// <param name="userName">    Name of the user to be associated with the transaction. </param>
        /// <param name="accountName"> Name of the account to be associated with the transaction. </param>
        /// <param name="productName"> Name of the product to be associated with the transaction. </param>
        public static void SetUserParameters(string? userName, string? accountName, string? productName)
        {
            _agentApiImplementation?.SetUserParameters(userName, accountName, productName);
        }

        /// <summary>
        /// Ignore the transaction that is currently in process.
        /// Supports web applications only.
        /// </summary>
        public static void IgnoreTransaction()
        {
            _agentApiImplementation?.IgnoreTransaction();
        }

        /// <summary>
        /// Ignore the current transaction in the apdex computation.
        /// Supports web applications only.
        /// </summary>
        public static void IgnoreApdex()
        {
            _agentApiImplementation?.IgnoreApdex();
        }

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
        public static string GetBrowserTimingHeader()
        {
            return _agentApiImplementation?.GetBrowserTimingHeader() ?? string.Empty;
        }

        /// <summary> Returns the HTML snippet to be inserted into the header of HTML pages to enable Real
        /// User Monitoring. The HTML will instruct the browser to fetch a small JavaScript file and
        /// start the page timer. Supports web applications only. </summary>
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
        public static string GetBrowserTimingHeader(string nonce)
        {
            return _agentApiImplementation?.GetBrowserTimingHeader(nonce) ?? string.Empty;
        }

        /// <summary> (This method is obsolete) gets browser timing footer. </summary>
        ///
        /// <returns> An empty string. </returns>
        [Obsolete]
        public static string GetBrowserTimingFooter()
        {
            return _agentApiImplementation?.GetBrowserTimingFooter() ?? string.Empty;
        }

        /// <summary>
        /// Disables the automatic instrumentation of browser monitoring hooks in individual pages
        /// Supports web applications only.
        /// </summary>
        /// <example>
        /// <code>
        /// NewRelic.Api.Agent.NewRelic.DisableBrowserMonitoring()
        /// </code>
        /// </example>
        public static void DisableBrowserMonitoring(bool overrideManual = false)
        {
            _agentApiImplementation?.DisableBrowserMonitoring(overrideManual);
        }

        /// <summary> (This API should only be used from the public API)  Starts the agent (i.e. begin
        /// capturing data). Does nothing if the agent is already started. Useful if agent autostart is
        /// disabled via configuration, or if you want to ensure the agent is started before using other
        /// methods in the Agent API. </summary>
        ///
        /// <example><code>
        ///   NewRelic.Api.Agent.NewRelic.StartAgent();
        /// </code></example>
        public static void StartAgent()
        {
            _agentApiImplementation?.StartAgent();
        }

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
        public static void SetApplicationName(string applicationName, string? applicationName2 = null, string? applicationName3 = null)
        {
            _agentApiImplementation?.SetApplicationName(applicationName, applicationName2, applicationName3);
        }

        /// <summary> Gets the request metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        public static IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            return _agentApiImplementation?.GetRequestMetadata() ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }

        /// <summary> Gets the response metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        public static IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            return _agentApiImplementation?.GetResponseMetadata() ?? Enumerable.Empty<KeyValuePair<string, string>>();
        }
    }
}

#nullable restore
