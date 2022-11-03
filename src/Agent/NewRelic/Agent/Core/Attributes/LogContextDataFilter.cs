// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes
{
    public interface ILogContextDataFilter
    {
        Dictionary<string, object> FilterLogContextData(Dictionary<string, object> contextData);
    }

    public class LogContextDataFilter : ILogContextDataFilter
    {
        private IConfigurationService _configurationService;
        private List<LogContextDataFilterRule> _includeRuleList;
        private List<LogContextDataFilterRule> _excludeRuleList;
        private List<LogContextDataFilterRule> _orderedCludeRuleList;

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
            // Create list of rules, sorted by least specific to most specific, with includes first and then excludes within each level of specificity
            // Specificity is just the length of the string, minus any wildcard character at the end
            // Apply rules in order. Include rules add attributes to filteredContextData from unfilteredContextData, and exclude rules remove attributes from filteredContextData

            var allRulesInOrder = _orderedCludeRuleList ?? (_orderedCludeRuleList = GetOrderedCludeRuleList());

            if (_includeRuleList.Count() == 0) // empty "include" list, so include everything
            {
                filteredContextData = unfilteredContextData;
            }

            // Now apply the rules in order
            // TODO: maybe optimize by just working with keys as lists of strings until it comes time to return the actual data
            foreach (var rule in allRulesInOrder)
            {
                if (rule.Include)
                {
                    filteredContextData = filteredContextData.Concat(unfilteredContextData.Where(x => Regex.IsMatch(x.Key, rule.Text))).ToDictionary();
                }
                else
                {
                    filteredContextData = filteredContextData.Where(x => !Regex.IsMatch(x.Key, rule.Text)).ToDictionary();
                }
            }
            return filteredContextData;
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

            // This should work because OrderBy uses a stable sort.  By sorting each list individually,
            // concactating them, and then sorting again, we get a list of rules ordered by length (specificity),
            // with includes before excludes for each level of specificity
            return includeRuleList.OrderBy(rule => rule.Specificity).Concat(
                excludeRuleList.OrderBy(rule => rule.Specificity))
                .OrderBy(rule => rule.Specificity).ToList();
        }

    }
    internal class LogContextDataFilterRule
    {
        string _text;
        bool _include;
        int _specificity; // just the length, except ignore a trailing wildcard.  I.e. AB is more specific than A*

        internal LogContextDataFilterRule(string text, bool include)
        {
            _text = text;
            _include = include;
            _specificity = text.TrimEnd('*').Length;
        }

        public string Text => _text;
        public bool Include => _include;
        public int Specificity => _specificity;
    }

}
