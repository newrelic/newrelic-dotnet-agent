// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Core.CodeAttributes;
using NewRelic.Core.Logging;
using System;
using System.Collections.Generic;

// The AgentApi is the only interface we expose to our customers.
//
// The public static members of this class must exactly match
// what is in the companion/parallel NewRelic.Api.Agent/NewRelic.cs file.
// Customers link with NewRelic.Api.Agent.dll, which comes from the companion NewRelic.Api.Agent/NewRelic.cs file
//
// The profiler arranges to change the body of member functions in NewRelic.Api.Agent/NewRelic.cs into
// calls to members in this file.
//
// The documentation we publish on the API is derived by XSLT transformations from the companion NewRelic.Api.Agent/NewRelic.cs file.
// So, to avoid drift and confusion, do NOT document the API in this file; document it in the NewRelic.Api.Agent/NewRelic.cs file.

// The namespace of this method CANNOT be changed. The profiler hard-codes "NewRelic.Agent.Core" as the expected namespace for the agent API.
namespace NewRelic.Agent.Core
{
    /// <summary>
    /// The interface we expose to our customers.
    /// All public functions here must be static.
    /// All public functions here must have the EXACTLY the same type signature as their stub counterparts in NewRelic.Api.Agent/NewRelic.cs
    /// </summary>
    public static class AgentApi
    {
        static AgentApi()
        {
            // Ensure the agent is initialized before any Agent API methods are used
            AgentInitializer.InitializeAgent();
        }

        private static IApiSupportabilityMetricCounters? _apiSupportabilityMetricCounters;

        public static void SetSupportabilityMetricCounters(IApiSupportabilityMetricCounters apiSupportabilityMetricCounters)
        {
            _apiSupportabilityMetricCounters = apiSupportabilityMetricCounters;
        }

        public static void InitializePublicAgent(object publicAgent)
        {
            InternalApi.InitializePublicAgent(publicAgent);
        }

        private static void LogApiError(string methodName, Exception ex)
        {
            try
            {
                Log.Warn($"Agent API Error: An error occurred invoking API method \"{methodName}\" - \"{ex}\"");
            }
            catch (Exception) // swallow errors
            {
            }
        }

        private static void RecordSupportabilityMetric(ApiMethod apiMethod)
        {
            _apiSupportabilityMetricCounters?.Record(apiMethod);
        }

        /// <summary> Increment the supportability metric counter for specific API. Update our thread state
        /// as performing work that should not be traced. Execute an action and catch/log any exceptions
        /// thrown by the action. </summary>
        /// <param name="action">	  An action to perform. </param>
        /// <param name="methodName"> A string identifying the API name. </param>
        /// <param name="apiMethod">  An enum value identifying the supportability metric to create. </param>
        private static void TryInvoke(Action action, string methodName, ApiMethod apiMethod)
        {
            try
            {
                using (new IgnoreWork())  //do not trace activity on this call tree
                {
                    RecordSupportabilityMetric(apiMethod);
                    action();
                }
            }
            catch (Exception ex)
            {
                LogApiError(methodName, ex);
            }
        }

        /// <summary> Increment the supportability metric counter for specific API. Update our thread state
        /// as performing work that should not be traced. Execute an function and catch/log any
        /// exceptions thrown by the action. On failure a default(T) is returned and no exceptions are
        /// thrown. </summary>
        /// <typeparam name="T"> Generic type parameter specifying the return type of the action. </typeparam>
        /// <param name="action">	  An action to perform. </param>
        /// <param name="methodName"> A string identifying the API name. </param>
        /// <param name="apiMethod">  An enum value identifying the supportability metric to create. </param>
        /// <returns> A value of the generic type T. </returns>
        public static T? TryInvoke<T>(Func<T> action, string methodName, ApiMethod apiMethod)
            where T : class
        {
            try
            {
                using (new IgnoreWork())  //do not trace activity on this call tree
                {
                    RecordSupportabilityMetric(apiMethod);
                    return action();
                }
            }
            catch (Exception ex)
            {
                LogApiError(methodName, ex);
            }
            return default(T);
        }

        /// <summary> Record a custom analytics event. </summary>
        /// <param name="eventType">  The name of the metric to record. Only the first 255 characters (256
        /// including the null terminator) are retained. </param>
        /// <param name="attributes"> The value to record. Only the first 1000 characters are retained. </param>
        public static void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            const ApiMethod apiMetric = ApiMethod.RecordCustomEvent;
            const string apiName = nameof(RecordCustomEvent);
            void work()
            {
                InternalApi.RecordCustomEvent(eventType, attributes);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary> Record a metric value for the given name. </summary>
        /// <param name="name">  The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="value"> The value to record. Only the first 1000 characters are retained. </param>
        public static void RecordMetric(string name, float value)
        {
            const ApiMethod apiMetric = ApiMethod.RecordMetric;
            const string apiName = nameof(RecordMetric);
            void work()
            {
                InternalApi.RecordMetric(name, value);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Record a response time in milliseconds for the given metric name.
        /// </summary>
        /// <param name="name">The name of the response time metric to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="millis">The response time to record in milliseconds.</param>
        public static void RecordResponseTimeMetric(string name, long millis)
        {
            const ApiMethod apiMetric = ApiMethod.RecordResponseTimeMetric;
            const string apiName = nameof(RecordResponseTimeMetric);
            void work()
            {
                InternalApi.RecordResponseTimeMetric(name, millis);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Increment the metric counter for the given name.
        /// </summary>
        /// <param name="name">The name of the metric to increment.
        /// Only the first 1000 characters are retained.
        /// </param>
        public static void IncrementCounter(string name)
        {
            const ApiMethod apiMetric = ApiMethod.IncrementCounter;
            const string apiName = nameof(IncrementCounter);
            void work()
            {
                InternalApi.IncrementCounter(name);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by an exception report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the exception/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only.
        /// </summary>
        /// <param name="exception">The exception to be reported.
        /// Only part of the exception's information may be retained to prevent the report from being too large.
        /// </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        public static void NoticeError(Exception exception, IDictionary<string, string>? customAttributes)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(exception, customAttributes);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by an exception report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the exception/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only.
        /// </summary>
        /// <param name="exception">The exception to be reported.
        /// Only part of the exception's information may be retained to prevent the report from being too large.
        /// </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        public static void NoticeError(Exception exception, IDictionary<string, object>? customAttributes)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(exception, customAttributes);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by an exception and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the exception/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only.
        /// </summary>
        /// <param name="exception">The exception to be reported.
        /// Only part of the exception's information may be retained to prevent the report from being too large.
        /// </param>
        public static void NoticeError(Exception exception)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(exception);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.
        /// </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        public static void NoticeError(string message, IDictionary<string, string>? customAttributes)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(message, customAttributes);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">Mark error as expected so that it won't affect Apdex score and error rate.</param>
        public static void NoticeError(string message, IDictionary<string, string>? customAttributes, bool isExpected)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(message, customAttributes, isExpected);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message.
        /// </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        public static void NoticeError(string message, IDictionary<string, object>? customAttributes)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(message, customAttributes);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">Mark error as expected so that it won't affect Apdex score and error rate.</param>
        public static void NoticeError(string message, IDictionary<string, object>? customAttributes, bool isExpected)
        {
            const ApiMethod apiMetric = ApiMethod.NoticeError;
            const string apiName = nameof(NoticeError);
            void work()
            {
                InternalApi.NoticeError(message, customAttributes, isExpected);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Add a key/value pair to the current transaction.  These are reported in errors and transaction traces.
        /// Supports web applications only.
        /// </summary>
        /// <param name="key">The key name to add to the transaction parameters.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The numeric value to add to the current transaction.</param>
        [Obsolete("This method does nothing in version 9.x+ of the Agent.  Use Transaction.AddCustomAttribute instead")]
        public static void AddCustomParameter(string key, IConvertible value)
        {
            try
            {
                Log.Warn("AddCustomParameter was called by an outdated version of the Agent API. Use Transaction.AddCustomAttribute instead.");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Add a key/value pair to the current transaction.  These are reported in errors and transaction traces.
        /// Supports web applications only.
        /// </summary>
        /// <param name="key">The key.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The value.
        /// Only the first 1000 characters are retained.
        /// </param>
        [Obsolete("This method does nothing in version 9.x+ of the Agent. Use Transaction.AddCustomAttribute instead")]
        public static void AddCustomParameter(string key, string value)
        {
            try
            {
                Log.Warn("AddCustomParameter was called by an outdated version of the Agent API. Use Transaction.AddCustomAttribute instead.");
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Set the name of the current transaction.
        /// Supports web applications only.
        /// </summary>
        /// <param name="category">The category of this transaction, used to distinguish different types of transactions.
        /// Defaults to <code>Custom</code>.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="name">The name of the transaction starting with a forward slash.  example: /store/order
        /// Only the first 1000 characters are retained.
        /// </param>
        public static void SetTransactionName(string? category, string name)
        {
            const ApiMethod apiMetric = ApiMethod.SetTransactionName;
            const string apiName = nameof(SetTransactionName);
            void work()
            {
                InternalApi.SetTransactionName(category, name);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Set the URI of the current transaction.
        /// Supports web applications only.
        /// </summary>
        /// <param name="uri">The URI of this transaction.</param>
        public static void SetTransactionUri(Uri uri)
        {
            const ApiMethod apiMetric = ApiMethod.SetTransactionUri;
            const string apiName = nameof(SetTransactionUri);
            void work()
            {
                InternalApi.SetTransactionUri(uri);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Sets the User Name, Account Name and Product Name to associate with the RUM JavaScript footer for the current web transaction.
        /// Supports web applications only.
        /// </summary>
        /// <param name="userName"> Name of the user to be associated with the transaction.</param>
        /// <param name="accountName">Name of the account to be associated with the transaction.</param>
        /// <param name="productName">Name of the product to be associated with the transaction.</param>
        public static void SetUserParameters(string? userName, string? accountName, string? productName)
        {
            const ApiMethod apiMetric = ApiMethod.SetUserParameters;
            const string apiName = nameof(SetUserParameters);
            void work()
            {
                InternalApi.SetUserParameters(userName, accountName, productName);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Ignore the transaction that is currently in process.
        /// Supports web applications only.
        /// </summary>
        public static void IgnoreTransaction()
        {
            const ApiMethod apiMetric = ApiMethod.IgnoreTransaction;
            const string apiName = nameof(IgnoreTransaction);
            TryInvoke(InternalApi.IgnoreTransaction, apiName, apiMetric);
        }

        /// <summary>
        /// Ignore the current transaction in the apdex computation.
        /// Supports web applications only.
        /// </summary>
        public static void IgnoreApdex()
        {
            const ApiMethod apiMetric = ApiMethod.IgnoreApdex;
            const string apiName = nameof(IgnoreApdex);
            TryInvoke(InternalApi.IgnoreApdex, apiName, apiMetric);
        }

        /// <summary>
        /// Returns the html snippet to be inserted into the header of html pages to enable Real User Monitoring.
        /// The html will instruct the browser to fetch a small JavaScript file and start the page timer.
        /// Supports web applications only.
        /// </summary>
        /// <example>
        /// <code>
        /// &lt;html>
        ///   &lt;head>
        ///     &lt;&#37;= NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader()&#37;>
        ///   &lt;/head>
        ///   &lt;body>
        ///   ...
        /// </code>
        /// </example>
        /// <returns>An html string to be embedded in a page header.</returns>
        public static string GetBrowserTimingHeader()
        {
            const ApiMethod apiMetric = ApiMethod.GetBrowserTimingHeader;
            const string apiName = nameof(GetBrowserTimingHeader);
            return TryInvoke(() => InternalApi.GetBrowserTimingHeader(), apiName, apiMetric) ?? string.Empty;
        }

        /// <summary>
        /// Returns the html snippet to be inserted into the header of html pages to enable Real User Monitoring.
        /// The html will instruct the browser to fetch a small JavaScript file and start the page timer.
        /// Supports web applications only.
        /// </summary>
        /// <param name="nonce">An optional per-request, cryptographic nonce used by a <c>Content-Security-Policy</c> <c>script-src</c> policy.</param>
        /// <example>
        /// <code>
        /// &lt;html>
        ///   &lt;head>
        ///     &lt;&#37;= NewRelic.Api.Agent.NewRelic.GetBrowserTimingHeader("random-nonce")&#37;>
        ///   &lt;/head>
        ///   &lt;body>
        ///   ...
        /// </code>
        /// </example>
        /// <returns>An html string to be embedded in a page header.</returns>
        public static string GetBrowserTimingHeader(string nonce)
        {
            const ApiMethod apiMetric = ApiMethod.GetBrowserTimingHeader;
            const string apiName = nameof(GetBrowserTimingHeader);
            return TryInvoke(() => InternalApi.GetBrowserTimingHeader(nonce), apiName, apiMetric) ?? string.Empty;
        }

        /// <summary>
        /// Obsolete method that used to return the html snippet to be inserted into the footer of html pages as part of Real User Monitoring.
        /// Now only returns and empty string.
        /// Supports web applications only.
        /// <returns>An empty string.</returns>
        [Obsolete("This method does nothing in version 9.x+ of the Agent.")]
        public static string GetBrowserTimingFooter()
        {
            try
            {
                Log.Warn("GetBrowserTimingFooter was called by an outdated version of the Agent API.");
            }
            catch (Exception)
            {
            }
            return string.Empty;
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
        /// <param name="overrideManual">(Optional) True to override manual instrumentation.</param>
        public static void DisableBrowserMonitoring(bool overrideManual = false)
        {
            const ApiMethod apiMetric = ApiMethod.DisableBrowserMonitoring;
            const string apiName = nameof(DisableBrowserMonitoring);
            void work()
            {
                InternalApi.DisableBrowserMonitoring(overrideManual);
            }
            TryInvoke(work, apiName, apiMetric);
        }

        /// <summary>
        /// Starts the agent (i.e. begin capturing data). Does nothing if the agent is already started.
        /// Useful if agent autostart is disabled via configuration, or if you want to ensure the agent
        /// is started before using other methods in the Agent API.
        /// </summary>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.StartAgent();
        /// </code>
        /// </example>
        public static void StartAgent()
        {
            const ApiMethod apiMetric = ApiMethod.StartAgent;
            const string apiName = nameof(StartAgent);
            TryInvoke(InternalApi.StartAgent, apiName, apiMetric);
        }

        /// <summary>
        /// Sets the name of the application to <paramref name="applicationName"/>. At least one given name must not be null.
        /// 
        /// An application may also have up to two additional names. This can be useful, for example, to have multiple 
        /// applications report under the same roll-up name.
        /// </summary>
        /// <param name="applicationName">The main application name.</param>
        /// <param name="applicationName2">An optional second application name.</param>
        /// <param name="applicationName3">An optional third application name.</param>
        public static void SetApplicationName(string applicationName, string? applicationName2 = null, string? applicationName3 = null)
        {
            const string apiName = nameof(SetApplicationName);
            void work()
            {
                InternalApi.SetApplicationName(applicationName, applicationName2, applicationName3);
            }

            TryInvoke(work, apiName, ApiMethod.SetApplicationName);
        }

        /// <summary>
        /// Get the request metadata for the current transaction.
        /// If Distributed Tracing is enabled this returns an empty list.
        /// </summary>
        /// <returns>A list of key-value pairs representing the request metadata.</returns>
        public static IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            return InternalApi.GetRequestMetadata() ?? new Dictionary<string, string>();
        }

        /// <summary>
        /// Get the response metadata for the current transaction.
        /// If Distributed Tracing is enabled this returns an empty list.
        /// </summary>
        /// <returns>A list of key-value pairs representing the response metadata.</returns>
        public static IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            return InternalApi.GetResponseMetadata() ?? new Dictionary<string, string>();
        }

        /// <summary> Sets the method that will be invoked to define the error group that an exception
        /// should belong to.
        ///
        /// The callback takes an an IReadOnlyDictionary of attributes, the stack trace, and Exception,
        /// and returns the name of the error group to use. Return values
        /// that are null, empty, or whitespace will not associate the Exception to an error group.
        /// </summary>
        /// <param name="callback">The callback to invoke to define the error group that an Exception belongs to.</param>
        public static void SetErrorGroupCallback(Func<IReadOnlyDictionary<string, object>, string> callback)
        {
            const string apiName = nameof(SetErrorGroupCallback);
            void work()
            {
                InternalApi.SetErrorGroupCallback(callback);
            }
            TryInvoke(work, apiName, ApiMethod.SetErrorGroupCallback);
        }
    }
}

#nullable restore
