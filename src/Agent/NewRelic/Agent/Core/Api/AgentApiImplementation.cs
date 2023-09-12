// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Core.CodeAttributes;
using NewRelic.Core.DistributedTracing;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Core.Logging;
using NewRelic.Agent.Api;
using System.Linq;

namespace NewRelic.Agent.Core.Api
{
    public class AgentApiImplementation : IAgentApi
    {
        private static readonly char[] TrimPathChar = new[] { MetricNames.PathSeparatorChar };
        private const string CustomMetricNamePrefixAndSeparator = MetricNames.Custom + MetricNames.PathSeparator;
        private const string DistributedTracingIsEnabledIgnoringCall = "Distributed tracing is enabled. Ignoring {0} call.";
        // Special case value for Path in ErrorTraceWireModel for errors outside Transactions
        private const string NoticeErrorPath = "NewRelic.Api.Agent.NoticeError API Call";

        private readonly ITransactionService _transactionService;
        private readonly ICustomEventTransformer _customEventTransformer;
        private readonly IMetricBuilder _metricBuilder;
        private readonly IMetricAggregator _metricAggregator;
        private readonly ICustomErrorDataTransformer _customErrorDataTransformer;
        private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
        private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
        private readonly IConfigurationService _configurationService;
        private readonly IAgent _agent;
        private readonly AgentBridgeApi _agentBridgeApi;
        private readonly ITracePriorityManager _tracePriorityManager;
        private readonly IErrorService _errorService;

        public AgentApiImplementation(ITransactionService transactionService, ICustomEventTransformer customEventTransformer, IMetricBuilder metricBuilder, IMetricAggregator metricAggregator, ICustomErrorDataTransformer customErrorDataTransformer, IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgent agent, ITracePriorityManager tracePriorityManager, IApiSupportabilityMetricCounters apiSupportabilityMetricCounters, IErrorService errorService)
        {
            _transactionService = transactionService;
            _customEventTransformer = customEventTransformer;
            _metricBuilder = metricBuilder;
            _metricAggregator = metricAggregator;
            _customErrorDataTransformer = customErrorDataTransformer;
            _browserMonitoringPrereqChecker = browserMonitoringPrereqChecker;
            _browserMonitoringScriptMaker = browserMonitoringScriptMaker;
            _configurationService = configurationService;
            _agent = agent;
            _agentBridgeApi = new AgentBridgeApi(_agent, apiSupportabilityMetricCounters, _configurationService);
            _tracePriorityManager = tracePriorityManager;
            _errorService = errorService;
        }

        public void InitializePublicAgent(object publicAgent)
        {
            try
            {
                using (new IgnoreWork())
                {
                    Log.Info("Initializing the Agent API");
                    var method = publicAgent.GetType().GetMethod("SetWrappedAgent", BindingFlags.NonPublic | BindingFlags.Instance);
                    method.Invoke(publicAgent, new[] { _agentBridgeApi });
                }
            }
            catch (Exception ex)
            {
                try
                {
                    Log.Error(ex, "Failed to initialize the Agent API");
                }
                catch (Exception)//Swallow the error
                {
                }
            }
        }

        /// <summary> Record a custom analytics event represented by a name and a list of key-value pairs. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="eventType"/> or
        /// <paramref name="attributes"/> are null. </exception>
        ///
        /// <param name="eventType">  The name of the event to record. Only the first 255 characters (256
        /// including the null terminator) are retained. </param>
        /// <param name="attributes"> The attributes to associate with this event. </param>
        public void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            eventType = eventType ?? throw new ArgumentNullException(nameof(eventType));
            attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                float priority = transaction?.Priority ?? _tracePriorityManager.Create();

                _customEventTransformer.Transform(eventType, attributes, priority);
            }
        }

        /// <summary> Record a named metric with the given duration. </summary>
        ///
        /// <param name="name">  The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="value"> The number of seconds to associate with the named attribute. This can be
        /// negative, 0, or positive. </param>
        public void RecordMetric(string name, float value)
        {
            using (new IgnoreWork())
            {
                var metricName = GetCustomMetricSuffix(name); //throws if name is null or empty
                var time = TimeSpan.FromSeconds(value); //throws if value is NaN, Neg Inf, or Pos Inf, < TimeSpan.MinValue, > TimeSpan.MaxValue
                var metric = _metricBuilder.TryBuildCustomTimingMetric(metricName, time);

                if (metric != null)
                {
                    _metricAggregator.Collect(metric);
                }
            }
        }

        /// <summary> Record response time metric. </summary>
        ///
        /// <param name="name">   The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        /// <param name="millis"> The milliseconds duration of the response time. </param>
        public void RecordResponseTimeMetric(string name, long millis)
        {
            using (new IgnoreWork())
            {
                var metricName = GetCustomMetricSuffix(name); //throws if name is null or empty
                var time = TimeSpan.FromMilliseconds(millis);
                var metric = _metricBuilder.TryBuildCustomTimingMetric(metricName, time);

                if (metric != null)
                {
                    _metricAggregator.Collect(metric);
                }
            }
        }

        /// <summary> Increment the metric counter for the given name. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="name"/> is null. </exception>
        ///
        /// <param name="name"> The name of the metric to record. Only the first 1000 characters are
        /// retained. </param>
        public void IncrementCounter(string name)
        {
            name = name ?? throw new ArgumentNullException(nameof(name));

            using (new IgnoreWork())
            {
                // NOTE: Unlike Custom timing metrics, Custom count metrics are NOT restricted to only the "Custom" namespace.
                // This is probably a historical blunder -- it's not a good thing that we allow users to use whatever text they want for the first segment.
                // However, that is what the API currently allows and it would be difficult to take that feature away.
                var metric = _metricBuilder.TryBuildCustomCountMetric(name);

                if (metric != null)
                {
                    _metricAggregator.Collect(metric);
                }
            }
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
        public void NoticeError(Exception exception, IDictionary<string, string>? customAttributes)
        {
            exception = exception ?? throw new ArgumentNullException(nameof(exception));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (IsCustomExceptionIgnored(exception, transaction)) return;
                var errorData = _errorService.FromException(exception, customAttributes);
                ProcessNoticedError(errorData, transaction);
            }
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
        public void NoticeError(Exception exception, IDictionary<string, object>? customAttributes)
        {
            exception = exception ?? throw new ArgumentNullException(nameof(exception));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (IsCustomExceptionIgnored(exception, transaction)) return;
                var errorData = _errorService.FromException(exception, customAttributes);
                ProcessNoticedError(errorData, transaction);
            }
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
        public void NoticeError(Exception exception)
        {
            exception = exception ?? throw new ArgumentNullException(nameof(exception));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (IsCustomExceptionIgnored(exception, transaction)) return;
                var errorData = _errorService.FromException(exception);
                ProcessNoticedError(errorData, transaction);
            }
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
        /// <param name="message">		    The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        public void NoticeError(string message, IDictionary<string, string>? customAttributes)
        {
            NoticeError(message, customAttributes, false);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        /// 
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">Mark error as expected so that it won't affect Apdex score and error rate.</param>
        public void NoticeError(string message, IDictionary<string, string>? customAttributes, bool isExpected)
        {
            message = message ?? throw new ArgumentNullException(nameof(message));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (IsErrorMessageIgnored(message)) return;
                var errorData = _errorService.FromMessage(message, customAttributes, isExpected);
                ProcessNoticedError(errorData, transaction);
            }
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
        /// <param name="message">		    The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes"> Custom parameters to include in the traced error. May be
        /// null. Only 10,000 characters of combined key/value data is retained. </param>
        public void NoticeError(string message, IDictionary<string, object>? customAttributes)
        {
            NoticeError(message, customAttributes, false);
        }

        /// <summary>
        /// Notice an error identified by a simple message and report it to the New Relic service.
        /// If this method is called within a transaction,
        /// the exception will be reported with the transaction when it finishes.  
        /// If it is invoked outside of a transaction, a traced error will be created and reported to the New Relic service.
        /// Only the string/parameter pair for the first call to NoticeError during the course of a transaction is retained.
        /// Supports web applications only. 
        /// </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="message"/> is null. </exception>
        /// 
        /// <param name="message">The message to be displayed in the traced error.
        /// This method creates both Error Events and Error Traces.
        /// Only the first 255 characters are retained in Error Events while Error Traces will retain the full message. </param>
        /// <param name="customAttributes">Custom parameters to include in the traced error.
        /// May be null.
        /// Only 10,000 characters of combined key/value data is retained.
        /// </param>
        /// <param name="isExpected">Mark error as expected so that it won't affect Apdex score and error rate.</param>
        public void NoticeError(string message, IDictionary<string, object>? customAttributes, bool isExpected)
        {
            message = message ?? throw new ArgumentNullException(nameof(message));

            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (IsErrorMessageIgnored(message)) return;
                var errorData = _errorService.FromMessage(message, customAttributes, isExpected);
                ProcessNoticedError(errorData, transaction);
            }
        }

        private void ProcessNoticedError(ErrorData errorData, IInternalTransaction transaction)
        {
            if (transaction != null)
            {
                transaction.NoticeError(errorData);
            }
            else
            {
                errorData.Path = NoticeErrorPath;
                _customErrorDataTransformer.Transform(errorData, _tracePriorityManager.Create(), null);
            }
        }

        private bool IsCustomExceptionIgnored(Exception exception, IInternalTransaction transaction)
        {
            if (_errorService.ShouldIgnoreException(exception))
            {
                if (transaction != null) transaction.TransactionMetadata.TransactionErrorState.SetIgnoreCustomErrors();
                return true;
            }

            return false;
        }

        private bool IsErrorMessageIgnored(string message)
        {
            // The agent does not currently implement ignoring errors by error message.
            // The spec allows for this functionality: https://source.datanerd.us/agents/agent-specs/blob/master/Errors.md#ignore--expected-errors
            return false;
        }

        /// <summary> Add a key/value pair to the current transaction.  These are reported in errors and
        /// transaction traces. Supports web applications only. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="key"/> or
        /// <paramref name="value"/> is null. </exception>
        ///
        /// <param name="key">   The key name for the custom parameter. </param>
        /// <param name="value"> The value associated with the custom parameter. If the value is a float
        /// it is recorded as a number, otherwise, <paramref name="value"/> is converted to a string.
        /// (via <c>value.ToString(CultureInfo.InvariantCulture);</c> </param>
        [ToBeRemovedInFutureRelease("Use TransactionBridgeApi.AddCustomAttribute(string, object) instead")]
        public void AddCustomParameter(string key, IConvertible value)
        {
            key = key ?? throw new ArgumentNullException(nameof(key));
            value = value ?? throw new ArgumentNullException(nameof(value));

            using (new IgnoreWork())
            {
                // float (32-bit) precision numbers are specially handled and actually stored as floating point numbers. Everything else is stored as a string. This is for historical reasons -- in the past Dirac only stored single-precision numbers, so integers and doubles had to be stored as strings to avoid losing precision. Now Dirac DOES support integers and doubles, but we can't just blindly start passing up integers and doubles where we used to pass strings because it could break customer queries.
                var normalizedValue = value is float
                    ? value
                    : value.ToString(CultureInfo.InvariantCulture);

                AddUserAttributeToCurrentTransaction(key, normalizedValue);
            }
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
        [ToBeRemovedInFutureRelease("Use TransactionBridgeApi.AddCustomAttribute(string, object) instead")]
        public void AddCustomParameter(string key, string value)
        {
            key = key ?? throw new ArgumentNullException(nameof(key));
            value = value ?? throw new ArgumentNullException(nameof(value));
            using (new IgnoreWork())
            {
                AddUserAttributeToCurrentTransaction(key, value);
            }
        }

        [ToBeRemovedInFutureRelease("Use TransactionBridgeApi.AddCustomAttribute(string, object) instead")]
        private void AddUserAttributeToCurrentTransaction(string key, object value)
        {
            if (_configurationService.Configuration.CaptureCustomParameters)
            {
                var transaction = GetCurrentInternalTransaction();
                transaction.AddCustomAttribute(key, value);
            }
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
        public void SetTransactionName(string? category, string name)
        {
            name = name ?? throw new ArgumentNullException(nameof(name));

            using (new IgnoreWork())
            {
                // Default to "Custom" category if none provided
                if (category == null || string.IsNullOrWhiteSpace(category))
                {
                    category = MetricNames.Custom;
                }

                // Get rid of any slashes
                category = category.Trim(TrimPathChar);
                name = name.Trim(TrimPathChar);

                // Clamp the category and name to a predetermined length
                category = Clamper.ClampLength(category);
                name = Clamper.ClampLength(name);

                var transaction = GetCurrentInternalTransaction();

                var currentTransactionName = transaction.CandidateTransactionName.CurrentTransactionName;

                var newTransactionName = currentTransactionName.IsWeb
                    ? TransactionName.ForWebTransaction(category, name)
                    : TransactionName.ForOtherTransaction(category, name);

                transaction.CandidateTransactionName.TrySet(newTransactionName, TransactionNamePriority.UserTransactionName);
            }
        }

        /// <summary> Sets transaction URI. </summary>
        ///
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="uri"/> is null. </exception>
        ///
        /// <param name="uri"> URI to be associated with the transaction. </param>
        public void SetTransactionUri(Uri uri)
        {
            uri = uri ?? throw new ArgumentNullException(nameof(uri));

            using (new IgnoreWork())
            {
                var transaction = _agent.CurrentTransaction;
                if (transaction != null)
                {
                    transaction.SetUri(uri.AbsolutePath);
                    transaction.SetOriginalUri(uri.AbsolutePath);
                    transaction.SetWebTransactionNameFromPath(WebTransactionType.Custom, uri.AbsolutePath);
                }
            }
        }

        /// <summary> Sets the User Name, Account Name and Product Name to associate with the RUM
        /// JavaScript footer for the current web transaction. Supports web applications only. </summary>
        ///
        /// <param name="userName">    Name of the user to be associated with the transaction. </param>
        /// <param name="accountName"> Name of the account to be associated with the transaction. </param>
        /// <param name="productName"> Name of the product to be associated with the transaction. </param>
        public void SetUserParameters(string? userName, string? accountName, string? productName)
        {
            using (new IgnoreWork())
            {
                if (_configurationService.Configuration.CaptureCustomParameters)
                {
                    //var transactionMetadata = GetCurrentInternalTransaction().TransactionMetadata;
                    var transaction = GetCurrentInternalTransaction();
                    if (userName != null && !string.IsNullOrEmpty(userName))
                    {
                        transaction.AddCustomAttribute("user", userName.ToString(CultureInfo.InvariantCulture));
                    }

                    if (accountName != null && !string.IsNullOrEmpty(accountName))
                    {
                        transaction.AddCustomAttribute("account", accountName.ToString(CultureInfo.InvariantCulture));
                    }

                    if (productName != null && !string.IsNullOrEmpty(productName))
                    {
                        transaction.AddCustomAttribute("product", productName.ToString(CultureInfo.InvariantCulture));
                    }
                }
            }
        }

        /// <summary> Ignore the transaction that is currently in process. Supports web applications only. </summary>
        public void IgnoreTransaction()
        {
            using (new IgnoreWork())
            {
                _agent.CurrentTransaction.Ignore();
            }
        }

        /// <summary> Ignore the current transaction in the apdex computation. Supports web applications
        /// only. </summary>
        public void IgnoreApdex()
        {
            using (new IgnoreWork())
            {
                GetCurrentInternalTransaction().IgnoreApdex();
            }
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
        public string GetBrowserTimingHeader()
        {
            return GetBrowserTimingHeader(string.Empty);
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
        public string GetBrowserTimingHeader(string nonce)
        {
            using (new IgnoreWork())
            {
                var transaction = TryGetCurrentInternalTransaction();
                if (transaction == null)
                    return string.Empty;

                var shouldInject = _browserMonitoringPrereqChecker.ShouldManuallyInject(transaction);
                if (!shouldInject)
                    return string.Empty;

                transaction.IgnoreAllBrowserMonitoringForThisTx();

                // The transaction's name must be frozen if we're going to generate a RUM script
                transaction.CandidateTransactionName.Freeze(TransactionNameFreezeReason.ManualBrowserScriptInjection);

                return _browserMonitoringScriptMaker.GetScript(transaction, nonce) ?? string.Empty;
            }
        }

        /// <summary> Disables the automatic instrumentation of browser monitoring hooks in individual
        /// pages Supports web applications only. </summary>
        ///
        /// <param name="overrideManual"> (Optional) True to override manual. </param>
        ///
        /// <example><code>
        /// NewRelic.Api.Agent.NewRelic.DisableBrowserMonitoring()
        /// </code></example>
        public void DisableBrowserMonitoring(bool overrideManual = false)
        {
            using (new IgnoreWork())
            {
                var transaction = GetCurrentInternalTransaction();

                if (overrideManual)
                    transaction.IgnoreAllBrowserMonitoringForThisTx();
                else
                    transaction.IgnoreAutoBrowserMonitoringForThisTx();
            }
        }

        /// <summary> (This API should only be used from the public API)  Starts the agent (i.e. begin
        /// capturing data). Does nothing if the agent is already started. Useful if agent autostart is
        /// disabled via configuration, or if you want to ensure the agent is started before using other
        /// methods in the Agent API. </summary>
        ///
        /// <example><code>
        ///   NewRelic.Api.Agent.NewRelic.StartAgent();
        /// </code></example>
        public void StartAgent()
        {
            using (new IgnoreWork())
            {
                EventBus<StartAgentEvent>.Publish(new StartAgentEvent());
            }
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
        public void SetApplicationName(string applicationName, string? applicationName2 = null, string? applicationName3 = null)
        {
            var appNames = new List<string>();
            if (applicationName != null)
            {
                appNames.Add(applicationName);
            }

            if (applicationName2 != null)
            {
                appNames.Add(applicationName2);
            }

            if (applicationName3 != null)
            {
                appNames.Add(applicationName3);
            }

            if (appNames.Count == 0)
            {
                throw new ArgumentException("At least one application name must be non-null.");
            }

            using (new IgnoreWork())
            {
                EventBus<AppNameUpdateEvent>.Publish(new AppNameUpdateEvent(appNames));
            }
        }

        private IInternalTransaction TryGetCurrentInternalTransaction()
        {
            return _transactionService.GetCurrentInternalTransaction();
        }

        /// <summary> Gets the current transaction. Throws an exception if a transaction could not be
        /// found. Use TryGetCurrentInternlTransaction if you prefer getting a null return. </summary>
        ///
        /// <exception cref="InvalidOperationException"> . </exception>
        ///
        /// <returns> A transaction. </returns>
        private IInternalTransaction GetCurrentInternalTransaction()
        {
            return TryGetCurrentInternalTransaction() ??
                throw new InvalidOperationException("The API method called is only valid from within a transaction. This error can occur if you call the API method from a thread other than the one the transaction started on.");
        }

        /// <summary> Gets custom metric suffix. </summary>
        /// <exception cref="ArgumentException"> Thrown if <paramref name="name"/> is null or empty. </exception>
        /// <param name="name"> The name to process. </param>
        /// <returns> The custom metric suffix. </returns>
        private static string GetCustomMetricSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("The name parameter must have a value that is not null or empty.");

            name = Clamper.ClampLength(name);

            // If the name provided already contains the "Custom/" prefix, remove it and use the remaining segment as the "name"
            if (name.StartsWith(CustomMetricNamePrefixAndSeparator, StringComparison.InvariantCultureIgnoreCase))
                name = name.Substring(CustomMetricNamePrefixAndSeparator.Length);

            return name;
        }

        /// <summary> Gets the request metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            if (_configurationService.Configuration.DistributedTracingEnabled)
            {
                Log.Finest(DistributedTracingIsEnabledIgnoringCall, nameof(GetRequestMetadata));
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            return _agent.CurrentTransaction.GetRequestMetadata();
        }

        /// <summary> Gets the response metadata for the current transaction. </summary>
        ///
        /// <returns> A list of key-value pairs representing the request metadata. </returns>
        public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            if (_configurationService.Configuration.DistributedTracingEnabled)
            {
                Log.Finest(DistributedTracingIsEnabledIgnoringCall, nameof(GetResponseMetadata));
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            return _agent.CurrentTransaction.GetResponseMetadata();
        }

        /// <summary> Sets the method that will be invoked to define the error group that an exception
        /// should belong to.
        ///
        /// The callback takes an an IReadOnlyDictionary of attributes, the stack trace, and Exception,
        /// and returns the name of the error group to use. Return values
        /// that are null, empty, or whitespace will not associate the Exception to an error group.
        /// </summary>
        /// <param name="callback">The callback to invoke to define the error group that an Exception belongs to.</param>
        public void SetErrorGroupCallback(Func<IReadOnlyDictionary<string, object>, string> callback)
        {
            using (new IgnoreWork())
            {
                EventBus<ErrorGroupCallbackUpdateEvent>.Publish(new ErrorGroupCallbackUpdateEvent(callback));
            }
        }
    }
}
#nullable restore
