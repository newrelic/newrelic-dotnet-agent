using System;
using System.Collections.Generic;
using NewRelic.Agent.Core.Api;

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
    public class AgentApi
    {
        static AgentApi()
        {
            // Ensure the agent is initialized before any Agent API methods are used
            AgentInitializer.InitializeAgent();
        }

        private static IAgentApi _agentApiImplementation;

        public const Int32 CustomTransactionNamePriority = 8;

        public static void SetAgentApiImplementation(IAgentApi agentApiImplementation)
        {
            _agentApiImplementation = agentApiImplementation;
        }

        /// <summary>
        /// Record a custom analytics event.
        /// </summary>
        /// <param name="eventType">The name of the metric to record.
        /// Only the first 255 characters (256 including the nul terminator) are retained.
        /// </param>
        /// <param name="attributes">The value to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        public static void RecordCustomEvent(String eventType, IEnumerable<KeyValuePair<String, Object>> attributes)
        {

            _agentApiImplementation?.RecordCustomEvent(eventType, attributes);
        }

        /// <summary>
        /// Record a metric value for the given name.
        /// </summary>
        /// <param name="name">The name of the metric to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The value to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        public static void RecordMetric(String name, Single value)
        {
            _agentApiImplementation?.RecordMetric(name, value);
        }

        /// <summary>
        /// Record a response time in milliseconds for the given metric name.
        /// </summary>
        /// <param name="name">The name of the response time metric to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="millis">The response time to record in milliseconds.</param>
        public static void RecordResponseTimeMetric(String name, Int64 millis)
        {
            _agentApiImplementation?.RecordResponseTimeMetric(name, millis);
        }

        /// <summary>
        /// Increment the metric counter for the given name.
        /// </summary>
        /// <param name="name">The name of the metric to increment.
        /// Only the first 1000 characters are retained.
        /// </param>
        public static void IncrementCounter(String name)
        {
            _agentApiImplementation?.IncrementCounter(name);
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
        public static void NoticeError(Exception exception, IDictionary<String, String> customAttributes)
        {
            _agentApiImplementation?.NoticeError(exception, customAttributes);
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
            _agentApiImplementation?.NoticeError(exception);
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
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        public static void NoticeError(String message, IDictionary<String, String> customAttributes)
        {
            _agentApiImplementation?.NoticeError(message, customAttributes);
        }

        /// <summary>
        /// Add a key/value pair to the current transaction.  These are reported in errors and transaction traces.
        /// Supports web applications only.
        /// </summary>
        /// <param name="key">The key name to add to the transaction parameters.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The numeric value to add to the current transaction.</param>
        public static void AddCustomParameter(String key, IConvertible value)
        {
            _agentApiImplementation?.AddCustomParameter(key, value);
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
        public static void AddCustomParameter(String key, String value)
        {
            _agentApiImplementation?.AddCustomParameter(key, value);
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
        public static void SetTransactionName(String category, String name)
        {
            _agentApiImplementation?.SetTransactionName(category, name);
        }

        public static void SetTransactionUri(Uri uri)
        {
            _agentApiImplementation?.SetTransactionUri(uri);
        }

        /// <summary>
        /// Sets the User Name, Account Name and Product Name to associate with the RUM JavaScript footer for the current web transaction.
        /// Supports web applications only.
        /// </summary>
        public static void SetUserParameters(String userName, String accountName, String productName)
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
        public static String GetBrowserTimingHeader()
        {
            return _agentApiImplementation?.GetBrowserTimingHeader() ?? String.Empty;
        }

        [Obsolete]
        public static String GetBrowserTimingFooter()
        {
            return _agentApiImplementation?.GetBrowserTimingFooter() ?? String.Empty;
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
        public static void DisableBrowserMonitoring(Boolean overrideManual = false)
        {
            _agentApiImplementation?.DisableBrowserMonitoring(overrideManual);
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
            _agentApiImplementation?.StartAgent();
        }

        /// <summary>
        /// Sets the name of the application to <paramref name="applicationName"/>. At least one given name must not be null.
        /// 
        /// An application may also have up to two additional names. This can be useful, for example, to have multiple 
        /// applications report under the same rollup name.
        /// </summary>
        /// <param name="applicationName">The main application name.</param>
        /// <param name="applicationName2">An optional second application name.</param>
        /// <param name="applicationName3">An optional third application name.</param>
        public static void SetApplicationName(String applicationName, String applicationName2 = null, String applicationName3 = null)
        {
            _agentApiImplementation?.SetApplicationName(applicationName, applicationName2, applicationName3);
        }

        public static IEnumerable<KeyValuePair<String, String>> GetRequestMetadata()
        {
            return _agentApiImplementation?.GetRequestMetadata() ?? new Dictionary<String, String>();
        }

        public static IEnumerable<KeyValuePair<String, String>> GetResponseMetadata()
        {
            return _agentApiImplementation?.GetResponseMetadata() ?? new Dictionary<String, String>();
        }

    }
}
