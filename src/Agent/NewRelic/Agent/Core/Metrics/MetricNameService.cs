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
        private static readonly TransactionMetricName NormalizedWebTransactionMetricName = new TransactionMetricName(MetricNames.WebTransactionPrefix, "Normalized/*");
        private static readonly TransactionMetricName NormalizedOtherTransactionMetricName = new TransactionMetricName(MetricNames.OtherTransactionPrefix, "Normalized/*");

        private readonly Utils.HashSet<string> _transactionNames = new Utils.HashSet<string>();

        #region Public API

        public string NormalizeUrl(string url)
        {
            url = StripParameters(url);
            url = RenameUsingRegexRules(url, _configuration.UrlRegexRules);

            return url;   
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
            var newPrefixedTransactionName = RenameUsingRegexRules(proposedTransactionName.PrefixedName, _configuration.TransactionNameRegexRules);

            if (newPrefixedTransactionName != null)
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

            if (!IsMetricNameAllowed(newPrefixedTransactionName))
                return proposedTransactionName.IsWebTransactionName ? NormalizedWebTransactionMetricName : NormalizedOtherTransactionMetricName;

            return newTransactionName;
        }

        public string RenameMetric(string metricName)
        {
            if (metricName == null)
                return null;

            return RenameUsingRegexRules(metricName, _configuration.MetricNameRegexRules);
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

        private static string RenameUsingRegexRules(string input, IEnumerable<RegexRule> rules)
        {
            foreach (var rule in rules.OrderBy(rule => rule.EvaluationOrder))
            {
                var ruleResult = rule.ApplyTo(input);
                if (!ruleResult.IsMatch)
                    continue;

                if (rule.Ignore)
                {
                    Log.Debug($"Ignoring \"{input}\" because it matched pattern \"{rule.MatchExpression}\"");
                    return null;
                }
                    
                if (ruleResult.Replacement == null)
                    throw new Exception("RuleResult matched but returned null replacement string");

                input = ruleResult.Replacement;

                if (rule.TerminateChain)
                    break;
            }

            return input;
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

        /// <summary>
        /// Checks if the given metric name is whitelisted according to the "black hole"
        /// 
        /// This logic is an implementation of the "black hole" rule.
        /// </summary>
        /// <param name="metricName"></param>
        /// <returns>True if the name is on (or is added to) the whitelist, else false</returns>
        private bool IsMetricNameAllowed(string metricName)
        {
            lock (_transactionNames)
            {
                // If metric name is already in whitelist, allow it through
                if (_transactionNames.Contains(metricName))
                    return true;

                // Otherwise, add the name to the whitelist and then allow it through
                _transactionNames.Add(metricName);

                return true;
            }
        }

        #endregion
    }
}

