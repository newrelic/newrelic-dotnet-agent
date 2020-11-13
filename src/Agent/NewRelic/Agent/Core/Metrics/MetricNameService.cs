// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Metric;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Metrics
{
    /// <summary>
    /// Normalizes transaction names using rules sent from the RPM service.
    /// </summary>
    public class MetricNameService : ConfigurationBasedService, IMetricNameService
    {
        #region Public API

        public string NormalizeUrl(string url)
        {
            url = StripParameters(url);
            if (TryRenameUsingRegexRules(url, _configuration.UrlRegexRules, out string result))
            {
                return result;
            }
            else
            {
                // This is currently a placeholder until a decision is made on
                // whether or not the pre-existing behavior of bubbling up an
                // exception is actually desired.
                return result;
            }  
        }

        public TimeSpan? TryGetApdex_t(string transactionName)
        {
            if (_configuration.WebTransactionsApdex.TryGetValue(transactionName, out double apdexT))
            {
                return TimeSpan.FromSeconds(Convert.ToSingle(apdexT));
            }
            return null;
        }

        public TransactionMetricName RenameTransaction(TransactionMetricName proposedTransactionName)
        {
            var shouldIgnore = false;

            if (TryRenameUsingRegexRules(proposedTransactionName.PrefixedName, _configuration.TransactionNameRegexRules, out string newPrefixedTransactionName))
            {
                newPrefixedTransactionName = RenameUsingWhitelistRules(newPrefixedTransactionName, _configuration.TransactionNameWhitelistRules);
            }
            else
            {
                shouldIgnore = true;
                newPrefixedTransactionName = proposedTransactionName.PrefixedName;
            }

            // Renaming rules are not allowed to change the first segment of a transaction name
            var newTransactionName = GetTransactionMetricName(newPrefixedTransactionName, proposedTransactionName, shouldIgnore);

            return newTransactionName;
        }

        public string RenameMetric(string metricName)
        {
            if (metricName == null)
                return null;

            TryRenameUsingRegexRules(metricName, _configuration.MetricNameRegexRules, out string result);

            return result;
        }

        #endregion Public API


        #region Event Handlers

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            // It is *CRITICAL* that this method never do anything more complicated than clearing data and starting and ending subscriptions.
            // If this method ends up trying to send data synchronously (even indirectly via the EventBus or RequestBus) then the user's application will deadlock (!!!).
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Takes a proposed prefixed transaction name as a string (e.g. "WebTransaction/Foo/Bar"), as well as an original transaction metric name, and returns a new TransactionMetricName. The proposed prefixed name will be converted to a metric name and returned iff it starts with the same prefix as the original metric name; otherwise, the original metric name will be returned.
        /// </summary>
        private static TransactionMetricName GetTransactionMetricName(string proposedPrefixedTransactionName, TransactionMetricName originalTransactionMetricName, bool shouldIgnore)
        {
            if (!proposedPrefixedTransactionName.StartsWith($"{originalTransactionMetricName.Prefix}{MetricNames.PathSeparator}"))
                return new TransactionMetricName(originalTransactionMetricName.Prefix, originalTransactionMetricName.UnPrefixedName, shouldIgnore);

            var proposedUnprefixedTransactionName = proposedPrefixedTransactionName.Substring(originalTransactionMetricName.Prefix.Length + 1);

            return new TransactionMetricName(originalTransactionMetricName.Prefix, proposedUnprefixedTransactionName, shouldIgnore);
        }

        private static string StripParameters(string url)
        {
            int index;
            if ((index = url.IndexOf('?')) > 0)
                return url.Substring(0, index);

            return url;
        }

        private static bool TryRenameUsingRegexRules(string input, IEnumerable<RegexRule> rules, out string result)
        {
            result = input;
            bool success = true;

            foreach (var rule in rules.OrderBy(rule => rule.EvaluationOrder))
            {
                var ruleResult = rule.ApplyTo(result);
                if (!ruleResult.IsMatch)
                {
                    continue;
                }
                else if (rule.Ignore)
                {
                    Log.DebugFormat($"Ignoring \"{input}\" because it matched pattern \"{rule.MatchExpression}\"");
                    result = null;
                    return false;
                }
                else if (ruleResult.Replacement == null)
                {
                    Log.Error("RuleResult matched but returned null replacement string");
                    result = null;
                    return false;
                }

                result = ruleResult.Replacement;

                if (rule.TerminateChain)
                    break;
            }

            return success;
        }

        private static string RenameUsingWhitelistRules(string metricName, IDictionary<string, IEnumerable<string>> whitelistRules)
        {
            if (!whitelistRules.Any())
                return metricName;

            var originalSegments = metricName.Split(MetricNames.PathSeparatorCharArray);
            if (originalSegments.Count() < 3 || (originalSegments.Count() == 3 && originalSegments[2] == ""))
                return metricName;

            var prefix = originalSegments[0] + MetricNames.PathSeparator + originalSegments[1];
            var allowedSegments = whitelistRules.GetValueOrDefault(prefix);
            if (allowedSegments == null)
                return metricName;

            var transformedSegments = originalSegments
                .Skip(2)
                .Select(segment => FilterSegment(segment, allowedSegments))
                .Unless((previous, current) => previous == "*" && current == "*");

            var allSegments = originalSegments.Take(2).Concat(transformedSegments);
            return string.Join(MetricNames.PathSeparator, allSegments.ToArray());
        }

        private static string FilterSegment(string segment, IEnumerable<string> allowedSegments)
        {
            if (allowedSegments.Contains(segment))
                return segment;

            return "*";
        }

        #endregion
    }
}

