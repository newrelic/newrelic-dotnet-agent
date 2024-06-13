// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.Attributes
{
    public interface IAttributeFilter
    {
        bool ShouldExcludeAttribute(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination);
        bool ShouldFilterAttribute(AttributeDestinations targetObjectType);
        bool CheckOrAddAttributeClusionCache(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination);
    }

    public class AttributeFilter : IAttributeFilter
    {
        private const uint MaxCacheSize = 1000;

        private readonly TrieNode<AttributeFilterNode> _explicitAttributeTrie;

        private readonly TrieNode<AttributeFilterNode> _implicitAttributeTrie;

        private readonly Settings _settings;

        private readonly ConcurrentDictionary<string, bool> _cachedClusions = new ConcurrentDictionary<string, bool>();

        public AttributeFilter(Settings settings)
        {
            _settings = settings;
            var explicitAttributeNodes = CreateExplicitAttributeNodes(settings);
            var implicitAttributeNodes = CreateImplicitAttributeNodes(settings);
            _explicitAttributeTrie = CreateAttributeNodeTrie(explicitAttributeNodes);
            _implicitAttributeTrie = CreateAttributeNodeTrie(implicitAttributeNodes);
        }

        public bool ShouldFilterAttribute(AttributeDestinations targetObjectType)
        {
            if (!_settings.AttributesEnabled)
                return true;

            switch (targetObjectType)
            {
                case AttributeDestinations.SpanEvent:
                    if (!_settings.SpanEventsEnabled)
                        return true;
                    break;
                case AttributeDestinations.ErrorTrace:
                    if (!_settings.ErrorTraceEnabled)
                        return true;
                    break;
                case AttributeDestinations.JavaScriptAgent:
                    if (!_settings.JavaScriptAgentEnabled)
                        return true;
                    break;
                case AttributeDestinations.TransactionEvent:
                    if (!_settings.TransactionEventEnabled)
                        return true;
                    break;
                case AttributeDestinations.TransactionTrace:
                    if (!_settings.TransactionTraceEnabled)
                        return true;
                    break;
                case AttributeDestinations.ErrorEvent:
                    if (!_settings.ErrorEventsEnabled)
                        return true;
                    break;
                case AttributeDestinations.SqlTrace:
                    break;
                case AttributeDestinations.CustomEvent:
                    if (!_settings.CustomEventsEnabled)
                        return true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("destination", "Unexpected destination: " + targetObjectType);
            }

            return false;
        }

        public bool CheckOrAddAttributeClusionCache(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination)
        {
            var cacheKey = GetAttributeClusionKey(name, defaultDestinations, destination);

            if (!_cachedClusions.TryGetValue(cacheKey, out var result))
            {
                result = !ShouldExcludeAttribute(name, defaultDestinations, destination);

                if (_cachedClusions.Count <= MaxCacheSize)
                {
                    _cachedClusions.TryAdd(cacheKey, result);
                }
            }

            return result;
        }

        private static string GetAttributeClusionKey(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination)
        {
            // Enum is cast to byte to avoid enum.ToString which does reflection 
            // The cache key includes both the intended destinations of the attribute (attribute.DefaultDestinations) 
            // and the destination being tested.  This is because some attributes have the same name (like timestamp)
            // but different destinations.  Without this cache key, the wrong value will be selected.
            return name + (((byte)destination << 8) + (byte)defaultDestinations).ToString();
        }

        public bool ShouldExcludeAttribute(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination)
        {
            var explicitClusion = CheckForExplicitClusion(name, destination);
            if (explicitClusion != Clude.Unknown)
                return KnownCludeToBoolean(explicitClusion);

            var implicitClusion = CheckForImplicitClusion(name, defaultDestinations, destination);
            if (implicitClusion != Clude.Unknown)
                return KnownCludeToBoolean(implicitClusion);

            return false;
        }

        private Clude CheckForExplicitClusion(string name, AttributeDestinations destination)
        {
            return _explicitAttributeTrie.GetClusion(name, destination);
        }

        private Clude CheckForImplicitClusion(string name, AttributeDestinations defaultDestinations, AttributeDestinations destination)
        {
            if ((defaultDestinations & destination) != destination)
                return Clude.Exclude;

            return _implicitAttributeTrie.GetClusion(name, destination);
        }

        private static bool KnownCludeToBoolean(Clude clude)
        {
            switch (clude)
            {
                case Clude.Exclude:
                    return true;
                case Clude.Include:
                    return false;
                case Clude.Unknown:
                    throw new Exception("Expected exclude or include but found Unknown.");
                default:
                    throw new Exception("Expected exclude or include but found " + clude);
            }
        }

        private static TrieNode<AttributeFilterNode> CreateAttributeNodeTrie(IEnumerable<AttributeFilterNode> nodes)
        {
            var trieBuilder = new TrieBuilder<AttributeFilterNode>(
                rootNodeDataFactory: () => new AttributeFilterNode("*", AttributeDestinations.None, AttributeDestinations.None),
                nodeDataMerger: MergeAttributeNodes,
                nodeDataComparor: CompareAttributeNodes,
                nodeDataHasher: HashAttributeNode,
                canParentAcceptChildChecker: CanParentAcceptChild,
                canNodeHaveChildrenChecker: CanNodeHaveChildren);

            return trieBuilder.CreateTrie(nodes);
        }

        private static IEnumerable<AttributeFilterNode> CreateExplicitAttributeNodes(Settings settings)
        {
            var globalIncludes = CreateAttributeNodes(settings.Includes, AttributeDestinations.All, true);
            var globalExcludes = CreateAttributeNodes(settings.Excludes, AttributeDestinations.All, false);
            var errorTraceIncludes = CreateAttributeNodes(settings.ErrorTraceIncludes, AttributeDestinations.ErrorTrace, true);
            var errorTraceExcludes = CreateAttributeNodes(settings.ErrorTraceExcludes, AttributeDestinations.ErrorTrace, false);
            var javaScriptAgentIncludes = CreateAttributeNodes(settings.JavaScriptAgentIncludes, AttributeDestinations.JavaScriptAgent, true);
            var javaScriptAgentExcludes = CreateAttributeNodes(settings.JavaScriptAgentExcludes, AttributeDestinations.JavaScriptAgent, false);
            var transactionEventIncludes = CreateAttributeNodes(settings.TransactionEventIncludes, AttributeDestinations.TransactionEvent, true);
            var transactionEventExcludes = CreateAttributeNodes(settings.TransactionEventExcludes, AttributeDestinations.TransactionEvent, false);
            var transactionTraceIncludes = CreateAttributeNodes(settings.TransactionTraceIncludes, AttributeDestinations.TransactionTrace, true);
            var transactionTraceExcludes = CreateAttributeNodes(settings.TransactionTraceExcludes, AttributeDestinations.TransactionTrace, false);
            var eventErrorIncludes = CreateAttributeNodes(settings.ErrorEventIncludes, AttributeDestinations.ErrorEvent, true);
            var eventErrorExcludes = CreateAttributeNodes(settings.ErrorEventExcludes, AttributeDestinations.ErrorEvent, false);
            var spanEventIncludes = CreateAttributeNodes(settings.SpanEventIncludes, AttributeDestinations.SpanEvent, true);
            var spanEventExcludes = CreateAttributeNodes(settings.SpanEventExcludes, AttributeDestinations.SpanEvent, false);

            return globalIncludes
                .Concat(globalExcludes)
                .Concat(errorTraceIncludes)
                .Concat(errorTraceExcludes)
                .Concat(javaScriptAgentIncludes)
                .Concat(javaScriptAgentExcludes)
                .Concat(transactionEventIncludes)
                .Concat(transactionEventExcludes)
                .Concat(transactionTraceIncludes)
                .Concat(transactionTraceExcludes)
                .Concat(eventErrorIncludes)
                .Concat(eventErrorExcludes)
                .Concat(spanEventIncludes)
                .Concat(spanEventExcludes);
        }

        private static IEnumerable<AttributeFilterNode> CreateImplicitAttributeNodes(Settings settings)
        {
            var globalIncludes = CreateAttributeNodes(settings.ImplicitIncludes, AttributeDestinations.All, true);
            var globalExcludes = CreateAttributeNodes(settings.ImplicitExcludes, AttributeDestinations.All, false);

            return globalIncludes.Concat(globalExcludes);
        }

        private static IEnumerable<AttributeFilterNode> CreateAttributeNodes(IEnumerable<string> keys, AttributeDestinations destinations, bool include)
        {
            return keys
                .Where(key => key != null)
                .Select(key => CreateAttributeNode(key, destinations, include));
        }

        private static AttributeFilterNode CreateAttributeNode(string key, AttributeDestinations destinations, bool include)
        {
            var includes = include ? destinations : AttributeDestinations.None;
            var excludes = include ? AttributeDestinations.None : destinations;
            return new AttributeFilterNode(key, includes, excludes);
        }

        private static AttributeFilterNode MergeAttributeNodes(IEnumerable<AttributeFilterNode> nodeBuilders)
        {
            var mergedNode = nodeBuilders.Aggregate(MergeAttributeNodes);
            if (mergedNode == null)
                throw new NullReferenceException("Attempt to merge attribute nodes yielded a null result.");
            return mergedNode;
        }

        private static AttributeFilterNode MergeAttributeNodes(AttributeFilterNode left, AttributeFilterNode right)
        {
            var key = left.Wildcard ? left.Key + "*" : left.Key;
            var includes = left.DestinationIncludes | right.DestinationIncludes;
            var excludes = left.DestinationExcludes | right.DestinationExcludes;
            return new AttributeFilterNode(key, includes, excludes);
        }

        private static int CompareAttributeNodes(AttributeFilterNode left, AttributeFilterNode right)
        {
            // keys are different, just use the key comparison result
            if (left.Key != right.Key)
            {
                return string.Compare(left.Key, right.Key, StringComparison.Ordinal);
            }

            // keys match and wildcard is the same, the attribute nodes are the same (possibly different rules, will need merging)
            if (left.Wildcard == right.Wildcard)
            {
                return 0;
            }

            // keys match, wildcards differ, the one with the wildcard comes first
            return left.Wildcard ? -1 : 1;
        }

        private static int HashAttributeNode(AttributeFilterNode nodeBuilder)
        {
            var suffix = nodeBuilder.Wildcard ? "*" : "";
            var stringToHash = nodeBuilder.Key + suffix;
            return stringToHash.GetHashCode();
        }

        private static bool CanParentAcceptChild(AttributeFilterNode parent, AttributeFilterNode orphan)
        {
            if (!parent.Wildcard)
            {
                return false;
            }

            if (!orphan.Key.StartsWith(parent.Key))
            {
                return false;
            }

            return true;
        }

        private static bool CanNodeHaveChildren(AttributeFilterNode node)
        {
            return node.Wildcard;
        }

        public class Settings
        {
            //Used for filtering intrinsic attributess (or not filtering them)
            public Settings()
            {
            }

            public Settings(IConfiguration _configuration)
            {
                AttributesEnabled = _configuration.CaptureAttributes;
                Includes = _configuration.CaptureAttributesIncludes;
                Excludes = _configuration.CaptureAttributesExcludes;

                ErrorTraceEnabled = _configuration.CaptureErrorCollectorAttributes;
                ErrorTraceIncludes = _configuration.CaptureErrorCollectorAttributesIncludes;
                ErrorTraceExcludes = _configuration.CaptureErrorCollectorAttributesExcludes;

                JavaScriptAgentEnabled = _configuration.CaptureBrowserMonitoringAttributes;
                JavaScriptAgentIncludes = _configuration.CaptureBrowserMonitoringAttributesIncludes;
                JavaScriptAgentExcludes = _configuration.CaptureBrowserMonitoringAttributesExcludes;

                TransactionEventEnabled = _configuration.TransactionEventsAttributesEnabled;
                TransactionEventIncludes = _configuration.TransactionEventsAttributesInclude;
                TransactionEventExcludes = _configuration.TransactionEventsAttributesExclude;

                TransactionTraceEnabled = _configuration.CaptureTransactionTraceAttributes;
                TransactionTraceIncludes = _configuration.CaptureTransactionTraceAttributesIncludes;
                TransactionTraceExcludes = _configuration.CaptureTransactionTraceAttributesExcludes;

                ErrorEventsEnabled = _configuration.ErrorCollectorCaptureEvents;
                ErrorEventIncludes = _configuration.CaptureErrorCollectorAttributesIncludes;
                ErrorEventExcludes = _configuration.CaptureErrorCollectorAttributesExcludes;

                SpanEventsEnabled = _configuration.SpanEventsEnabled;
                SpanEventIncludes = _configuration.SpanEventsAttributesInclude;
                SpanEventExcludes = _configuration.SpanEventsAttributesExclude;

                CustomEventsEnabled = _configuration.CustomEventsEnabled;
                CustomEventIncludes = _configuration.CustomEventsAttributesInclude;
                CustomEventExcludes = _configuration.CustomEventsAttributesExclude;
            }

            public bool AttributesEnabled = true;
            public IEnumerable<string> Excludes = Enumerable.Empty<string>();
            public IEnumerable<string> Includes = Enumerable.Empty<string>();

            public bool JavaScriptAgentEnabled = true;
            public IEnumerable<string> JavaScriptAgentExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> JavaScriptAgentIncludes = Enumerable.Empty<string>();

            public bool ErrorTraceEnabled = true;
            public IEnumerable<string> ErrorTraceExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> ErrorTraceIncludes = Enumerable.Empty<string>();

            public bool TransactionEventEnabled = true;
            public IEnumerable<string> TransactionEventExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> TransactionEventIncludes = Enumerable.Empty<string>();

            public bool TransactionTraceEnabled = true;
            public IEnumerable<string> TransactionTraceExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> TransactionTraceIncludes = Enumerable.Empty<string>();

            public IEnumerable<string> ImplicitExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> ImplicitIncludes = Enumerable.Empty<string>();

            public IEnumerable<string> ErrorEventExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> ErrorEventIncludes = Enumerable.Empty<string>();
            public bool ErrorEventsEnabled = true;

            public IEnumerable<string> SpanEventExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> SpanEventIncludes = Enumerable.Empty<string>();
            public bool SpanEventsEnabled = true;

            public IEnumerable<string> CustomEventExcludes = Enumerable.Empty<string>();
            public IEnumerable<string> CustomEventIncludes = Enumerable.Empty<string>();
            public bool CustomEventsEnabled = true;

        }
    }


}
