// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

// This file is compiled into a dll which is shipped to the customer,
// and ends up in, say:
//   C:\Program Files\NewRelic .NET Agent\NewRelic.Api.Agent.dll
// The customer will link to NewRelic.Api.Agent.dll, and will make calls to the public static functions herein.
// The function bodies here appear to do basically nothing.
//
// There is a companion file NewRelic.Core/NewRelic.Agent.Core/AgentApi.cs which is where the real work gets done.
// The NewRelic .NET agent profiler is special cased to short-circuit the bodies of the functions here
// with calls to the equivalent functions in NewRelic.Core/NewRelic.Agent.Core/AgentApi.cs.
//
// The documentation we publish for the API is derived by XSLT transformation from this file.
// As such, the method documentation here MUST be clear, descriptive, helpful, accurate and up-to-date.
// There's no point in documenting the companion functions in the NewRelic.Core/NewRelic.Agent.Core/Agentapi.cs file.
//
// All public functions here must have the EXACTLY the same type signature
// as their non-stub counterparts in NewRelic.Core/NewRelic.Agent.Core/Agentapi.cs file.
namespace NewRelic.Api.Agent
{
    /// <summary>
    /// The New Relic .NET Agent Api supports custom error and metric reporting, and custom transaction parameters.
    /// To use this library, add a reference to NewRelic.Agent.Api.dll to your project.  If the
    /// agent is not installed or is disabled, method invocations of this api will have no effect.
    /// 
    /// </summary>
    public static class NewRelic
    {
        static NewRelic()
        {
            InitializePublicAgent(_publicAgent);
        }

        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        private static void InitializePublicAgent(object publicAgent)
        {
            System.Diagnostics.Trace.WriteLine($"NewRelic.InitializePublicAgent({publicAgent})");
        }

        private static readonly IAgent _publicAgent = new Agent();

        // We are not disabling inlining and optimization of this method because our profiler
        // is not rewriting the implementation of this method.
        /// <summary>
        /// Get access to the Agent via the IAgent interface.
        /// </summary>
        /// <example>
        /// <code>
        ///   IAgent agent = GetAgent();
        /// </code>
        /// </example>
        public static IAgent GetAgent() { return _publicAgent; }

        #region Metric API

        /// <summary>
        /// Record a metric value for the given name.
        /// </summary>
        /// <param name="name">The name of the metric to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The time in seconds to record.</param>
        /// <example>
        /// <code>
        ///   DateTime start = DateTime.Now;
        ///   this.DelayTransaction(5000);
        ///   TimeSpan ts = DateTime.Now.Subtract(start);
        ///   NewRelic.Api.Agent.NewRelic.RecordMetric("Custom/DEMO_Record_Metric", ts.Seconds);
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void RecordMetric(string name, float value)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.RecordMetric({0},{1})", name, value));
        }

        /// <summary>
        /// Record a response time in milliseconds for the given metric name.
        /// </summary>
        /// <param name="name">The name of the response time metric to record.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="millis">The response time to record in milliseconds.</param>
        /// <example>
        /// <code>
        ///   DateTime start = DateTime.Now;
        ///   this.DelayTransaction(5000);
        ///   TimeSpan ts = DateTime.Now.Subtract(start);
        ///   NewRelic.Api.Agent.NewRelic.RecordResponseTimeMetric("Custom/DEMO_Record_Response_Time_Metric", ts.Milliseconds);
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void RecordResponseTimeMetric(string name, long millis)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.RecordResponseTimeMetric({0},{1})", name, millis));
        }

        /// <summary>
        /// Increment the metric counter for the given name.
        /// </summary>
        /// <param name="name">The name of the metric to increment.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.IncrementCounter("IncrementCounter");
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void IncrementCounter(string name)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.IncrementCounter({0})", name));
        }

        #endregion

        #region Error Collector

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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex, quotes);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(Exception exception, IDictionary<string, string>? parameters)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1})", exception, parameters));
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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex, quotes);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(Exception exception, IDictionary<string, object>? parameters)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1})", exception, parameters));
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
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(Exception exception)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0})", exception));
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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex.Message, quotes);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(string message, IDictionary<string, string>? parameters)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1})", message, parameters));
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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex.Message, quotes);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(string message, IDictionary<string, object>? parameters)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1})", message, parameters));
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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">
        /// Mark an error as expected.
        /// </param>>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex.Message, quotes, true);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(string message, IDictionary<string, string>? parameters, bool isExpected)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1},{2})", message, parameters, isExpected));
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
        /// <param name="parameters">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">
        /// Mark an error as expected.
        /// </param>>
        /// <example>
        /// <code>
        ///  try
        /// {
        ///    var ImNotABool = "43";
        ///    bool.Parse(ImNotABool);
        /// }
        /// catch (Exception ex)
        /// {
        ///    var quotes = new Dictionary&lt;string,string&gt;();
        ///    quotes.Add("1", "They had a large chunk of the garbage file? How much do they know?");
        ///    quotes.Add("2", "I'll hack the Gibson.");
        ///    quotes.Add("3", "Zero Cool? Crashed fifteen hundred and seven systems in one day?");
        ///    quotes.Add("4", "Turn on your laptop. Set it to receive a file.");
        ///    quotes.Add("5", "Listen you guys, help yourself to anything in the fridge. Cereal has.");
        ///    NewRelic.Api.Agent.NewRelic.NoticeError(ex.Message, quotes, true);
        /// }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void NoticeError(string message, IDictionary<string, object>? parameters, bool isExpected)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.NoticeError({0},{1},{2})", message, parameters, isExpected));
        }

        #endregion

        #region Transaction APIs

        /// <summary>
        /// Add a key/value pair to the current transaction.  These are reported in errors and transaction traces.
        /// Supports web applications only.
        /// </summary>
        /// <param name="key">The key name to add to the transaction parameters.
        /// Only the first 1000 characters are retained.
        /// </param>
        /// <param name="value">The numeric value to add to the current transaction.</param>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.AddCustomParameter("UserGuid", 1234);
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        [Obsolete("This method will be deprecated in future versions of the API.  Use GetAgent().CurrentTransaction.AddCustomAttribute(string, object) instead.")]
        public static void AddCustomParameter(string key, IConvertible value)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.AddCustomParameter({0},{1})", key, value));
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
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.AddCustomParameter("UserCultureSetting", "En-US");
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        [Obsolete("This method will be deprecated in future versions of the API.  Use GetAgent().CurrentTransaction.AddCustomAttribute(string, object) instead.")]
        public static void AddCustomParameter(string key, string value)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.AddCustomParameter({0},{1})", key, value));
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
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.SetTransactionName("Other", "MyTransaction");
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SetTransactionName(string? category, string name)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.SetTransactionName({0},{1})", category, name));
        }

        /// <summary>
        /// Sets the uri of a web transaction.  This should be used when instrumenting custom web
        /// frameworks using the WebTransaction attribute.
        /// </summary>
        /// <param name="uri">The uri of the web transaction</param>
        public static void SetTransactionUri(Uri uri)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.SetUri({0})", uri));
        }

        /// <summary>
        /// Ignore the transaction that is currently in process.
        /// Supports web applications only.
        /// </summary>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.IgnoreTransaction();
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void IgnoreTransaction()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.IgnoreTransaction()");
        }

        /// <summary>
        /// Ignore the current transaction in the apdex computation.
        /// Supports web applications only.
        /// </summary>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.IgnoreApdex();
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void IgnoreApdex()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.IgnoreApdex()");
        }

        #endregion

        #region Real User Monitoring

        /// <summary>
        /// Returns the html snippet to be inserted into the header of html pages to enable Real User Monitoring.
        /// The html will instruct the browser to fetch a small JavaScript file and start the page timer.
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
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static string GetBrowserTimingHeader()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.GetBrowserTimingHeader()");
            return "<!-- New Relic Header -->";
        }

        /// <summary>
        /// Returns the html snippet to be inserted into the footer of html pages to enable Real User Monitoring.
        /// </summary>
        /// <example>
        /// <code>
        /// ...
        ///     &lt;&#37;= NewRelic.Api.Agent.NewRelic.GetBrowserTimingFooter()&#37;>
        ///   &lt;/body>
        /// &lt;/html>
        /// </code>
        /// </example>
        /// <returns>An html string to be embedded at the bottom of an html page.</returns>
        [Obsolete]
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static string GetBrowserTimingFooter()
        {
            return "<!-- New Relic Footer is Obsolete -->";
        }

        /// <summary>
        /// Disables the automatic instrumentation of browser monitoring hooks in individual pages.  
        /// This call should be added on any pages where RUM scripts are not needed or wanted. NOTE: This API call should be put as close as possible to the top of the view where you want RUM disabled.
        /// Set the optional parameter overrideManual to false to completely override all RUM script injection.
        /// </summary>
        /// <param name="overrideManual">The optional parameter overrideManual to false to completely override all RUM script injection.
        /// </param>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.DisableBrowserMonitoring();
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void DisableBrowserMonitoring(bool overrideManual = false)
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.DisableBrowserMonitoring()");
        }

        /// <summary>
        /// Sets the User Name, Account Name and Product Name to associate with the RUM JavaScript footer for the current web transaction.
        /// </summary>
        /// <param name="userName">The name or User Name of the current user  that is meaningful in the context of the current applciation
        /// </param>
        /// <param name="accountName">The name of the account that is meaningful in the context of the current applciation
        /// </param>
        /// <param name="productName">The name of the product that is meaningful in the context of the current applciation
        /// </param>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.SetUserParameters("MyUserName", "MyAccountName", "MyProductName");
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SetUserParameters(string? userName, string? accountName, string? productName)
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.SetUserParameters(String userName, String accountName, String productName)");
        }

        #endregion

        #region Custom Event API

        /// <summary>
        /// Record a custom analytics event.
        /// </summary>
        /// <param name="eventType">The name of the metric to record.
        /// Only the first 255 characters (256 including the nul terminator) are retained.
        /// </param>
        /// <param name="attributes">The value to record.
        /// Only the first 1000 characters are retained, and it must conform to the regexp given here: /^[a-zA-Z0-9:_ ]+$/
        /// </param>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            System.Diagnostics.Trace.WriteLine(string.Format("NewRelic.RecordCustomEvent({0},{1})", eventType, attributes));
        }

        #endregion

        #region Application API

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
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void StartAgent()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.StartAgent()");
        }

        /// <summary>
        /// Sets the name of the application to <paramref name="applicationName"/>.
        /// 
        /// An application may also have up to two additional names. This can be useful (for example) to have multiple 
        /// applications report under the same rollup name in addition to their own unique names.
        /// </summary>
        /// <param name="applicationName">The name to report under. Must not be null.</param>
        /// <param name="applicationName2">An optional second application name.</param>
        /// <param name="applicationName3">An optional third application name.</param>
        /// <example>
        /// <code>
        ///   NewRelic.Api.Agent.NewRelic.SetApplicationName("MyFirstApplication", "MyApplications");
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static void SetApplicationName(string applicationName, string? applicationName2 = null, string? applicationName3 = null)
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.SetApplicationName(String applicationName, String applicationName2 = null, String applicationName3 = null)");
        }

        /// <summary>
        /// Returns a set of KeyValuePairs representing the custom HTTP request
        /// headers that applications instrumented by NewRelic include in their 
        /// outbound external requests.  If the target application is also instrumented
        /// by New Relic, these headers are authenticated there and elicit a response.
        /// Together, this information is used to generate Cross Application Tracing 
        /// metrics and Service Maps.
        /// </summary>
        /// <returns></returns>
        /// <example>
        /// <code>
        ///   var requestMetadata = NewRelic.Api.Agent.NewRelic.GetRequestMetadata();
        ///   foreach (var item in requestMetadata)
        ///	  {
        ///		  request.Headers.Add(item.Key, item.Value);
        ///	  }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.GetRequestMetadata()");
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        /// <summary>
        /// Returns a set of KeyValuePairs representing the custom HTTP response
        /// headers that applications instrumented by NewRelic include in their 
        /// response to incoming requests from other applications instrumented by
        /// New Relic.  Normally these headers are automatically generated, but you
        /// may need to manually include them in HTTP responses if you have Web Api 
        /// Custom Message Handlers which finalize the response by reading its content.
        /// In that case, use this method to manually obtain the response headers, then 
        /// manually add them to your response to ensure proper generation of Cross 
        /// Application Tracing metrics and Service Maps. 
        /// </summary>
        /// <returns></returns>
        /// <example>
        /// <code>
        ///   var responseMetadata = NewRelic.Api.Agent.NewRelic.GetResponseMetadata();
        ///   foreach (var item in responseMetadata)
        ///	  {
        ///		  response.Headers.Add(item.Key, item.Value);
        ///	  }
        /// </code>
        /// </example>
        [MethodImpl(MethodImplOptions.NoOptimization | MethodImplOptions.NoInlining)]
        public static IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            System.Diagnostics.Trace.WriteLine("NewRelic.GetResponseMetadata()");
            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        #endregion
    }

    /// <summary>
    /// Adding this attribute to a method will instruct the New Relic agent to time invocations of the method that
    /// occur within a transaction.  When the method is invoked outside of the context of a transaction, no
    /// measurements will be recorded.
    /// 
    /// A metric representing the timing measurements will be reporting inside the call scope of the current transaction
    /// so that New Relic can "break out" the response time of a given transaction by specific called
    /// methods.  A rollup summary metric (all invocations of the method for every transaction) will also be reported.
    /// 
    /// Be mindful when using this attribute.  When placed on relatively heavyweight operations such as database access or
    /// webservice invocation, its overhead will be negligible.   If placed on a tight, frequently called
    /// method (e.g.an accessor that is called thousands of times per second), then the tracer will introduce higher
    /// overhead to your application.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TraceAttribute : Attribute
    {
    }

    /// <summary>
    /// Instructs the New Relic agent to create a transaction and time the associated method.  The behavior of
    /// this attribute is identical to the Trace attribute when the method is invoked within the context of an
    /// existing transaction.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public class TransactionAttribute : TraceAttribute
    {
        /// <summary>
        /// If true, the transaction will be reported as a web transaction, otherwise it
        /// will be reported as an "other" transaction.  The default is false.
        /// </summary>
        public bool Web { get; set; }
    }
}
