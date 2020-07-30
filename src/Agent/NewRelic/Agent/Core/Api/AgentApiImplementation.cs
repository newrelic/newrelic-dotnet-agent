/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MoreLinq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Aggregators;
using NewRelic.Agent.Core.BrowserMonitoring;
using NewRelic.Agent.Core.Errors;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using ITransaction = NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders.ITransaction;


namespace NewRelic.Agent.Core.Api
{
    public class AgentApiImplementation : IAgentApi
    {
        private const string CustomMetricNamePrefixAndSeparator = MetricNames.Custom + MetricNames.PathSeparator;
        private readonly ITransactionService _transactionService;
        private readonly IAgentHealthReporter _agentHealthReporter;
        private readonly ICustomEventTransformer _customEventTransformer;
        private readonly IMetricBuilder _metricBuilder;
        private readonly IMetricAggregator _metricAggregator;
        private readonly ICustomErrorDataTransformer _customErrorDataTransformer;
        private readonly IBrowserMonitoringPrereqChecker _browserMonitoringPrereqChecker;
        private readonly IBrowserMonitoringScriptMaker _browserMonitoringScriptMaker;
        private readonly IConfigurationService _configurationService;

        private readonly IAgentWrapperApi _agentWrapperApi;

        public AgentApiImplementation(ITransactionService transactionService, IAgentHealthReporter agentHealthReporter, ICustomEventTransformer customEventTransformer, IMetricBuilder metricBuilder, IMetricAggregator metricAggregator, ICustomErrorDataTransformer customErrorDataTransformer, IBrowserMonitoringPrereqChecker browserMonitoringPrereqChecker, IBrowserMonitoringScriptMaker browserMonitoringScriptMaker, IConfigurationService configurationService, IAgentWrapperApi agentWrapperApi)
        {
            _transactionService = transactionService;
            _agentHealthReporter = agentHealthReporter;
            _customEventTransformer = customEventTransformer;
            _metricBuilder = metricBuilder;
            _metricAggregator = metricAggregator;
            _customErrorDataTransformer = customErrorDataTransformer;
            _browserMonitoringPrereqChecker = browserMonitoringPrereqChecker;
            _browserMonitoringScriptMaker = browserMonitoringScriptMaker;
            _configurationService = configurationService;
            _agentWrapperApi = agentWrapperApi;
        }

        public void RecordCustomEvent(string eventType, IEnumerable<KeyValuePair<string, object>> attributes)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(RecordCustomEvent));

                    if (eventType == null)
                        throw new ArgumentNullException(nameof(eventType));
                    if (attributes == null)
                        throw new ArgumentNullException(nameof(attributes));

                    _customEventTransformer.Transform(eventType, attributes);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(RecordCustomEvent), ex);
            }
        }

        public void RecordMetric(string name, float value)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(RecordMetric));

                    var metricName = GetCustomMetricSuffix(name);
                    var time = TimeSpan.FromSeconds(value);
                    var metric = _metricBuilder.TryBuildCustomTimingMetric(metricName, time);

                    if (metric != null)
                        _metricAggregator.Collect(metric);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(RecordMetric), ex);
            }
        }

        public void RecordResponseTimeMetric(string name, long millis)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(RecordResponseTimeMetric));

                    var metricName = GetCustomMetricSuffix(name);
                    var time = TimeSpan.FromMilliseconds(millis);
                    var metric = _metricBuilder.TryBuildCustomTimingMetric(metricName, time);

                    if (metric != null)
                        _metricAggregator.Collect(metric);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(RecordResponseTimeMetric), ex);
            }
        }

        public void IncrementCounter(string name)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(IncrementCounter));

                    if (name == null)
                        throw new ArgumentNullException(nameof(name));

                    // NOTE: Unlike Custom timing metrics, Custom count metrics are NOT restricted to only the "Custom" namespace.
                    // This is probably a historical blunder -- it's not a good thing that we allow users to use whatever text they want for the first segment.
                    // However, that is what the API currently allows and it would be difficult to take that feature away.
                    var metric = _metricBuilder.TryBuildCustomCountMetric(name);

                    if (metric != null)
                        _metricAggregator.Collect(metric);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(IncrementCounter), ex);
            }
        }

        public void NoticeError(Exception exception, IDictionary<string, string> customAttributes)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(NoticeError));

                    if (exception == null)
                        throw new ArgumentNullException(nameof(exception));

                    var stripErrorMessage = _configurationService.Configuration.HighSecurityModeEnabled;
                    var errorData = ErrorData.FromException(exception, stripErrorMessage);

                    var transaction = TryGetCurrentInternalTransaction();
                    if (transaction != null)
                    {
                        transaction.TransactionMetadata.AddCustomErrorData(errorData);

                        customAttributes?
                            .Where(attr => attr.Key != null && attr.Value != null)
                            .ForEach(attr => transaction.TransactionMetadata.AddUserErrorAttribute(attr.Key, attr.Value));
                    }
                    else
                    {
                        _customErrorDataTransformer.Transform(errorData, customAttributes);
                    }
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(NoticeError), ex);
            }
        }

        public void NoticeError(Exception exception)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(NoticeError));

                    if (exception == null)
                        throw new ArgumentNullException(nameof(exception));

                    var stripErrorMessage = _configurationService.Configuration.HighSecurityModeEnabled;
                    var errorData = ErrorData.FromException(exception, stripErrorMessage);

                    var transaction = TryGetCurrentInternalTransaction();
                    if (transaction != null)
                    {
                        transaction.TransactionMetadata.AddCustomErrorData(errorData);
                    }
                    else
                    {
                        _customErrorDataTransformer.Transform(errorData);
                    }
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(NoticeError), ex);
            }
        }

        public void NoticeError(string message, IDictionary<string, string> customAttributes)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(NoticeError));

                    if (message == null)
                        throw new ArgumentNullException(nameof(message));

                    var stripErrorMessage = _configurationService.Configuration.HighSecurityModeEnabled;
                    var errorData = ErrorData.FromParts(message, "Custom Error", DateTime.UtcNow, stripErrorMessage);

                    var transaction = TryGetCurrentInternalTransaction();
                    if (transaction != null)
                    {
                        transaction.TransactionMetadata.AddCustomErrorData(errorData);

                        customAttributes?
                            .Where(attr => attr.Key != null && attr.Value != null)
                            .ForEach(attr => transaction.TransactionMetadata.AddUserErrorAttribute(attr.Key, attr.Value));
                    }
                    else
                    {
                        _customErrorDataTransformer.Transform(errorData, customAttributes);
                    }
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(NoticeError), ex);
            }
        }

        public void AddCustomParameter(string key, IConvertible value)
        {
            try
            {
                using (new IgnoreWork())
                {
                    if (key == null)
                        throw new ArgumentNullException(nameof(key));
                    if (value == null)
                        throw new ArgumentNullException(nameof(value));

                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(AddCustomParameter));

                    // Single (32-bit) precision numbers are specially handled and actually stored as floating point numbers. Everything else is stored as a string. This is for historical reasons -- in the past Dirac only stored single-precision numbers, so integers and doubles had to be stored as strings to avoid losing precision. Now Dirac DOES support integers and doubles, but we can't just blindly start passing up integers and doubles where we used to pass strings because it could break customer queries.
                    var normalizedValue = value is float
                        ? value
                        : value.ToString(CultureInfo.InvariantCulture);

                    var transaction = GetCurrentInternalTransaction();
                    transaction.TransactionMetadata.AddUserAttribute(key, normalizedValue);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(AddCustomParameter), ex);
            }
        }

        public void AddCustomParameter(string key, string value)
        {
            try
            {
                using (new IgnoreWork())
                {
                    if (key == null)
                        throw new ArgumentNullException(nameof(key));
                    if (value == null)
                        throw new ArgumentNullException(nameof(value));

                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(AddCustomParameter));

                    var transaction = GetCurrentInternalTransaction();
                    transaction.TransactionMetadata.AddUserAttribute(key, value);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(AddCustomParameter), ex);
            }
        }

        public void SetTransactionName(string category, string name)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(SetTransactionName));

                    if (name == null)
                        throw new ArgumentNullException(nameof(name));

                    // Default to "Custom" category if none provided
                    if (string.IsNullOrEmpty(category?.Trim()))
                        category = MetricNames.Custom;

                    // Get rid of any slashes
                    category = category.Trim(MetricNames.PathSeparatorChar);
                    name = name.Trim(MetricNames.PathSeparatorChar);

                    // Clamp the category and name to a pre-determined length
                    category = Clamper.ClampLength(category);
                    name = Clamper.ClampLength(name);

                    var transaction = GetCurrentInternalTransaction();
                    var currentTranasctionName = transaction.CandidateTransactionName.CurrentTransactionName;
                    var newTransactionName = currentTranasctionName.IsWeb
                        ? new WebTransactionName(category, name)
                        : new OtherTransactionName(category, name) as ITransactionName;
                    transaction.CandidateTransactionName.TrySet(newTransactionName, AgentApi.CustomTransactionNamePriority);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(SetTransactionName), ex);
            }
        }

        public void SetTransactionUri(Uri uri)
        {
            try
            {
                var transaction = _agentWrapperApi.CurrentTransaction;
                transaction.SetUri(uri.AbsoluteUri);
                transaction.SetOriginalUri(uri.AbsoluteUri);
                transaction.SetWebTransactionNameFromPath(WebTransactionType.Custom, uri.AbsolutePath);
            }
            catch (Exception ex)
            {
                LogApiError(nameof(SetTransactionUri), ex);
            }
        }

        public void SetUserParameters(string userName, string accountName, string productName)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(SetUserParameters));

                    var transaction = GetCurrentInternalTransaction();

                    if (!string.IsNullOrEmpty(userName))
                        transaction.TransactionMetadata.AddUserAttribute("user", userName.ToString(CultureInfo.InvariantCulture));

                    if (!string.IsNullOrEmpty(accountName))
                        transaction.TransactionMetadata.AddUserAttribute("account", accountName.ToString(CultureInfo.InvariantCulture));

                    if (!string.IsNullOrEmpty(productName))
                        transaction.TransactionMetadata.AddUserAttribute("product", productName.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(SetUserParameters), ex);
            }
        }

        public void IgnoreTransaction()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(IgnoreTransaction));

                    _agentWrapperApi.CurrentTransaction.Ignore();
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(IgnoreTransaction), ex);
            }
        }

        public void IgnoreApdex()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(IgnoreApdex));

                    var transaction = GetCurrentInternalTransaction();
                    transaction.IgnoreApdex();
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(IgnoreApdex), ex);
            }
        }

        public string GetBrowserTimingHeader()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(GetBrowserTimingHeader));

                    var transaction = TryGetCurrentInternalTransaction();
                    if (transaction == null)
                        return string.Empty;

                    var shouldInject = _browserMonitoringPrereqChecker.ShouldManuallyInject(transaction);
                    if (!shouldInject)
                        return string.Empty;

                    transaction.IgnoreAllBrowserMonitoringForThisTx();

                    // The transaction's name must be frozen if we're going to generate a RUM script
                    transaction.CandidateTransactionName.Freeze();

                    return _browserMonitoringScriptMaker.GetScript(transaction);
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(GetBrowserTimingHeader), ex);
                return string.Empty;
            }
        }

        [Obsolete]
        public string GetBrowserTimingFooter()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(GetBrowserTimingFooter));

                    // This method is deprecated.
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(GetBrowserTimingFooter), ex);
                return string.Empty;
            }
        }

        public void DisableBrowserMonitoring(bool overrideManual = false)
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(DisableBrowserMonitoring));

                    var transaction = GetCurrentInternalTransaction();

                    if (overrideManual)
                        transaction.IgnoreAllBrowserMonitoringForThisTx();
                    else
                        transaction.IgnoreAutoBrowserMonitoringForThisTx();
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(DisableBrowserMonitoring), ex);
            }
        }

        public void StartAgent()
        {
            try
            {
                using (new IgnoreWork())
                {
                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(StartAgent));

                    EventBus<StartAgentEvent>.Publish(new StartAgentEvent());
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(StartAgent), ex);
            }
        }

        public void SetApplicationName(string applicationName, string applicationName2 = null, string applicationName3 = null)
        {
            try
            {
                using (new IgnoreWork())
                {
                    if (applicationName == null && applicationName2 == null && applicationName3 == null)
                        throw new ArgumentNullException(nameof(applicationName));

                    _agentHealthReporter.ReportAgentApiMethodCalled(nameof(SetApplicationName));

                    var appNames = new List<string> { applicationName, applicationName2, applicationName3 }
                        .Where(name => name != null);

                    EventBus<AppNameUpdateEvent>.Publish(new AppNameUpdateEvent(appNames));
                }
            }
            catch (Exception ex)
            {
                LogApiError(nameof(SetApplicationName), ex);
            }
        }

        private ITransaction TryGetCurrentInternalTransaction()
        {
            return _transactionService.GetCurrentInternalTransaction();
        }

        /// <summary>
        /// Gets the current transaction.
        /// Throws an exception if a transaction could not be found. Use TryGetCurrentInternlTransaction if you prefer getting a null return.
        /// </summary>
        /// <returns>A transaction.</returns>
        /// <exception cref="InvalidOperationException"/>
        private ITransaction GetCurrentInternalTransaction()
        {
            var transaction = TryGetCurrentInternalTransaction();

            if (transaction == null)
            {
                throw new InvalidOperationException("The API method called is only valid from within a transaction. This error can occur if you call the API method from a thread other than the one the transaction started on.");
            }

            return transaction;
        }

        private static void LogApiError(string methodName, Exception ex)
        {
            try
            {
                Log.WarnFormat("Agent API Error: An error occurred invoking API method \"{0}\" - \"{1}\"", methodName, ex);
            }
            catch (Exception)
            {
                // swallow errors
            }
        }
        private static string GetCustomMetricSuffix(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("The Name parameter must have a value that is not null or empty.");

            name = Clamper.ClampLength(name);

            // If the name provided already contains the "Custom/" prefix, remove it and use the remaning segment as the "name"
            if (name.StartsWith(CustomMetricNamePrefixAndSeparator, StringComparison.InvariantCultureIgnoreCase))
                name = name.Substring(CustomMetricNamePrefixAndSeparator.Length);

            return name;
        }

        public IEnumerable<KeyValuePair<string, string>> GetRequestMetadata()
        {
            return _agentWrapperApi.CurrentTransaction.GetRequestMetadata();
        }

        public IEnumerable<KeyValuePair<string, string>> GetResponseMetadata()
        {
            return _agentWrapperApi.CurrentTransaction.GetResponseMetadata();
        }

    }
}
