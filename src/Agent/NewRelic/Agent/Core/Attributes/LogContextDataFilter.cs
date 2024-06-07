// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Core.Logging;

namespace NewRelic.Agent.Core.Attributes
{
    public interface ILogContextDataFilter : IDisposable
    {
        Dictionary<string, object> FilterLogContextData(Dictionary<string, object> contextData);
    }

    public class LogContextDataFilter : ConfigurationBasedService, ILogContextDataFilter
    {
        private IConfigurationService _configurationService;
        private List<LogContextDataFilterRule> _includeRuleList;
        private List<LogContextDataFilterRule> _excludeRuleList;
        private List<LogContextDataFilterRule> _orderedCludeRuleList;

        private ConcurrentDictionary<string, bool> _clusionCache = new ConcurrentDictionary<string, bool>();
        private const int MaxCacheSize = 1000;
        private bool _clusionCacheSizeExceededWarningLogged = false;

        public LogContextDataFilter(IConfigurationService configurationService)
        {
            _configurationService = configurationService;
        }


        public Dictionary<string, object> FilterLogContextData(Dictionary<string, object> unfilteredContextData)
        {
            if (unfilteredContextData == null)
            {
                return null;
            }

            var filteredContextData = new Dictionary<string, object>();

            // Pseudocode
            // Include/exclude rules:
            // 1. Exclude wins over include
            // 2. Can have a '*' wildcard at the end of an include/exclude definition
            // 3. "more specific" rules win over "less specific".  Example: include=AB, exclude=A*, AB would be included but not ABC or AAA

            // Algorithm:
            // Create list of rules, sorted by most specific to least specific, with excludes first and then includes within each level of specificity
            // Specificity is just the length of the string, minus any wildcard character at the end
            // For each context data key/value pair:
            //   First, check the "clusion cache" to see if we already know whether or not to include or exclude this key, and if so handle it appropriately
            //   Otherwise, go through the list of rules and see if we find a matching rule to exclude or include, cache the result, and handle it appropriately

            foreach (var kvp in unfilteredContextData)
            {
                bool clusionResult;
                if (!_clusionCache.TryGetValue(kvp.Key, out clusionResult))
                {
                    clusionResult = GetClusionResult(kvp.Key);
                    if (_clusionCache.Count < MaxCacheSize)
                    {
                        _clusionCache[kvp.Key] = clusionResult;
                    }
                    else
                    {
                        if (!_clusionCacheSizeExceededWarningLogged)
                        {
                            Log.Warn($"LogContextDataFilter: max # ({MaxCacheSize}) of log context data attribute name inclusion/exclusion results reached.");
                            _clusionCacheSizeExceededWarningLogged = true;
                        }
                    }
                }

                if (clusionResult)
                {
                    filteredContextData[kvp.Key] = kvp.Value;
                }
            }

            return filteredContextData;
        }

        private bool GetClusionResult(string key)
        {
            var allRulesInOrder = _orderedCludeRuleList ?? (_orderedCludeRuleList = GetOrderedCludeRuleList());

            bool clusionResult = _includeRuleList.Count == 0; // An empty include rule list means include everything
            foreach (var rule in allRulesInOrder)
            {
                if (RuleMatches(rule, key))
                {
                    clusionResult = rule.Include;
                    break; // Because of how the rule list is ordered, we are done checking as soon as we find a match
                }
            }
            return clusionResult;
        }

        private bool RuleMatches(LogContextDataFilterRule rule, string text)
        {
            return rule.IsWildCard ? text.StartsWith(rule.Text, StringComparison.Ordinal) : text == rule.Text;
        }

        private List<LogContextDataFilterRule> GetCludeRulesFromConfig(bool isIncludeRules)
        {
            List<LogContextDataFilterRule> rules = new List<LogContextDataFilterRule>();
            var configRuleList = isIncludeRules ? _configurationService.Configuration.ContextDataInclude : _configurationService.Configuration.ContextDataExclude;
            foreach (var configRule in configRuleList)
            {
                rules.Add(new LogContextDataFilterRule(configRule, isIncludeRules));
            }
            return rules;
        }

        private List<LogContextDataFilterRule> GetOrderedCludeRuleList()
        {
            var includeRuleList = _includeRuleList ?? (_includeRuleList = GetCludeRulesFromConfig(true));
            var excludeRuleList = _excludeRuleList ?? (_excludeRuleList = GetCludeRulesFromConfig(false));

            // This works because OrderBy uses a stable sort.  By sorting each list individually,
            // concactenating them, and then sorting again, we get a list of rules ordered by length (specificity) descending,
            // with excludes before includes for each level of specificity
            return excludeRuleList.OrderByDescending(rule => rule.Specificity).Concat(
                includeRuleList.OrderByDescending(rule => rule.Specificity))
                .OrderByDescending(rule => rule.Specificity).ToList();
        }

        protected override void OnConfigurationUpdated(ConfigurationUpdateSource configurationUpdateSource)
        {
            _includeRuleList = null;
            _excludeRuleList = null;
            _orderedCludeRuleList = null;
            _clusionCache = new ConcurrentDictionary<string, bool>();
            _clusionCacheSizeExceededWarningLogged = false;
        }
    }
    public class LogContextDataFilterRule
    {
        string _text;
        bool _include;
        int _specificity; // just the length, except ignore a trailing wildcard.  I.e. AB is more specific than A*
        bool _isWildCard;

        public LogContextDataFilterRule(string text, bool include)
        {
            _isWildCard = text.EndsWith("*");
            _text = text.TrimEnd('*');
            _include = include;
            _specificity = _text.Length;
        }

        public string Text => _text;
        public bool Include => _include;
        public int Specificity => _specificity;
        public bool IsWildCard => _isWildCard;
    }

}
