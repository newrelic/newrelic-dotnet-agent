using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MoreLinq;
using NewRelic.Collections;
using NewRelic.SystemExtensions.Threading;
using NewRelic.Trie;

namespace NewRelic.Agent
{
    public class AttributeFilter<T> : IAttributeFilter<T> where T : IAttribute
    {
        private const UInt32 MaxCacheSize = 1000;
        private readonly TrieNode<AttributeFilterNode> _explicitAttributeTrie;
        private readonly TrieNode<AttributeFilterNode> _implicitAttributeTrie;
        private readonly Settings _settings;
        private readonly IDictionary<String, Boolean> _cachedClusions = new ConcurrentDictionary<String, Boolean>();

        public AttributeFilter(Settings settings)
        {
            _settings = settings;
            var explicitAttributeNodes = CreateExplicitAttributeNodes(settings);
            var implicitAttributeNodes = CreateImplicitAttributeNodes(settings);
            _explicitAttributeTrie = CreateAttributeNodeTrie(explicitAttributeNodes);
            _implicitAttributeTrie = CreateAttributeNodeTrie(implicitAttributeNodes);
        }

        public IEnumerable<T> FilterAttributes(IEnumerable<T> attributes, AttributeDestinations destination)
        {
            if (!_settings.AttributesEnabled)
                return Enumerable.Empty<T>();

            switch (destination)
            {
                case AttributeDestinations.ErrorTrace:
                    if (!_settings.ErrorTraceEnabled)
                        return Enumerable.Empty<T>();
                    break;
                case AttributeDestinations.JavaScriptAgent:
                    if (!_settings.JavaScriptAgentEnabled)
                        return Enumerable.Empty<T>();
                    break;
                case AttributeDestinations.TransactionEvent:
                    if (!_settings.TransactionEventEnabled)
                        return Enumerable.Empty<T>();
                    break;
                case AttributeDestinations.TransactionTrace:
                    if (!_settings.TransactionTraceEnabled)
                        return Enumerable.Empty<T>();
                    break;
                case AttributeDestinations.ErrorEvent:
                    if (!_settings.ErrorEventsEnabled)
                        return Enumerable.Empty<T>();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("destination", "Unexpected destination: " + destination);
            }

            var filteredAttributes = new List<T>();
            attributes
                .Where(attribute => attribute != null)
                .Where(attribute => ShouldIncludeAttribute(attribute, destination))
                .ForEach(filteredAttributes.Add);

            return filteredAttributes;
        }

        private Boolean ShouldIncludeAttribute(T attribute, AttributeDestinations destination)
        {
            var cachedClusion = CheckAttributeClusionCache(attribute, destination);
            if (cachedClusion != null)
                return cachedClusion.Value;

            var result = !ShouldExcludeAttribute(attribute, destination);

            AddToAttributeClusionCache(attribute, destination, result);

            return result;
        }

        private Boolean? CheckAttributeClusionCache(T attribute, AttributeDestinations destination)
        {
            var cacheKey = GetAttributeClusionKey(attribute, destination);
            if (_cachedClusions.TryGetValue(cacheKey, out Boolean cachedClusion))
            {
                return cachedClusion;
            }

            return null;
        }

        private void AddToAttributeClusionCache(T attribute, AttributeDestinations destination, Boolean result)
        {
            if (_cachedClusions.Count > MaxCacheSize)
                return;

            var cacheKey = GetAttributeClusionKey(attribute, destination);
            _cachedClusions[cacheKey] = result;
        }
        private static String GetAttributeClusionKey(T attribute, AttributeDestinations destination)
        {
            // Enum is cast to INT to avoid enum.ToString which does reflection 
            // Since its only used as a key converting to INT should be fine.  
            return attribute.Key + ((int)destination).ToString();
        }

        private Boolean ShouldExcludeAttribute(T attribute, AttributeDestinations destination)
        {
            var explicitClusion = CheckForExplicitClusion(attribute, destination);
            if (explicitClusion != Clude.Unknown)
                return KnownCludeToBoolean(explicitClusion);

            var implicitClusion = CheckForImplicitClusion(attribute, destination);
            if (implicitClusion != Clude.Unknown)
                return KnownCludeToBoolean(implicitClusion);

            return false;
        }

        private Clude CheckForExplicitClusion(T attribute, AttributeDestinations destination)
        {
            return _explicitAttributeTrie.GetClusion(attribute, destination);
        }

        private Clude CheckForImplicitClusion(T attribute, AttributeDestinations destination)
        {
            if ((attribute.DefaultDestinations & destination) != destination)
                return Clude.Exclude;

            return _implicitAttributeTrie.GetClusion(attribute, destination);
        }

        private static Boolean KnownCludeToBoolean(Clude clude)
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
                .Concat(eventErrorExcludes);
        }
        private static IEnumerable<AttributeFilterNode> CreateImplicitAttributeNodes(Settings settings)
        {
            var globalIncludes = CreateAttributeNodes(settings.ImplicitIncludes, AttributeDestinations.All, true);
            var globalExcludes = CreateAttributeNodes(settings.ImplicitExcludes, AttributeDestinations.All, false);

            return globalIncludes.Concat(globalExcludes);
        }
        private static IEnumerable<AttributeFilterNode> CreateAttributeNodes(IEnumerable<String> keys, AttributeDestinations destinations, Boolean include)
        {
            return keys
                .Where(key => key != null)
                .Select(key => CreateAttributeNode(key, destinations, include));
        }
        private static AttributeFilterNode CreateAttributeNode(String key, AttributeDestinations destinations, Boolean include)
        {
            var includes = (include) ? destinations : AttributeDestinations.None;
            var excludes = (include) ? AttributeDestinations.None : destinations;
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
            var key = (left.Wildcard) ? left.Key + "*" : left.Key;
            var includes = left.DestinationIncludes | right.DestinationIncludes;
            var excludes = left.DestinationExcludes | right.DestinationExcludes;
            return new AttributeFilterNode(key, includes, excludes);
        }

        private static Int32 CompareAttributeNodes(AttributeFilterNode left, AttributeFilterNode right)
        {
            // keys are different, just use the key comparison result
            if (left.Key != right.Key)
                return String.Compare(left.Key, right.Key, StringComparison.Ordinal);

            // keys match and wildcard is the same, the attribute nodes are the same (possibly different rules, will need merging)
            if (left.Wildcard == right.Wildcard)
                return 0;

            // keys match, wildcards differ, the one with the wildcard comes first
            if (left.Wildcard)
                return -1;
            else
                return 1;
        }

        private static Int32 HashAttributeNode(AttributeFilterNode nodeBuilder)
        {
            var suffix = (nodeBuilder.Wildcard) ? "*" : "";
            var stringToHash = nodeBuilder.Key + suffix;
            return stringToHash.GetHashCode();
        }

        private static Boolean CanParentAcceptChild(AttributeFilterNode parent, AttributeFilterNode orphan)
        {
            if (!parent.Wildcard)
                return false;

            if (!orphan.Key.StartsWith(parent.Key))
                return false;

            return true;
        }

        private static Boolean CanNodeHaveChildren(AttributeFilterNode node)
        {
            return node.Wildcard;
        }

        public class Settings
        {
            public Boolean AttributesEnabled = true;
            public IEnumerable<String> Excludes = Enumerable.Empty<String>();
            public IEnumerable<String> Includes = Enumerable.Empty<String>();

            public Boolean JavaScriptAgentEnabled = true;
            public IEnumerable<String> JavaScriptAgentExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> JavaScriptAgentIncludes = Enumerable.Empty<String>();

            public Boolean ErrorTraceEnabled = true;
            public IEnumerable<String> ErrorTraceExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> ErrorTraceIncludes = Enumerable.Empty<String>();

            public Boolean TransactionEventEnabled = true;
            public IEnumerable<String> TransactionEventExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> TransactionEventIncludes = Enumerable.Empty<String>();

            public Boolean TransactionTraceEnabled = true;
            public IEnumerable<String> TransactionTraceExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> TransactionTraceIncludes = Enumerable.Empty<String>();
            public IEnumerable<String> ImplicitExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> ImplicitIncludes = Enumerable.Empty<String>();
            public IEnumerable<String> ErrorEventExcludes = Enumerable.Empty<String>();
            public IEnumerable<String> ErrorEventIncludes = Enumerable.Empty<String>();
            public Boolean ErrorEventsEnabled = true;

        }
    }
}
