using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Trie;

namespace NewRelic.Agent
{
	public class AttributeFilter<T> : IAttributeFilter<T> where T : IAttribute
	{
		private const UInt32 MaxCacheSize = 1000;

		[NotNull]
		private readonly TrieNode<AttributeFilterNode> _explicitAttributeTrie;

		[NotNull]
		private readonly TrieNode<AttributeFilterNode> _implicitAttributeTrie;

		[NotNull]
		private readonly Settings _settings;

		[NotNull]
		private readonly ConcurrentDictionary<string, bool> _cachedClusions = new ConcurrentDictionary<string, bool>();

		public AttributeFilter([NotNull] Settings settings)
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
				case AttributeDestinations.SqlTrace:
					break;
				default:
					throw new ArgumentOutOfRangeException("destination", "Unexpected destination: " + destination);
			}

			
			var filteredAttributes = new List<T>();
			foreach (var attr in attributes)
			{
				if (ShouldIncludeAttribute(attr, destination))
				{
					filteredAttributes.Add(attr);
				}
			}

			return filteredAttributes;
		}

		
		private Boolean ShouldIncludeAttribute([NotNull] T attribute, AttributeDestinations destination)
		{
			var cachedClusion = CheckAttributeClusionCache(attribute, destination);
			if (cachedClusion != null)
				return cachedClusion.Value;

			var result = !ShouldExcludeAttribute(attribute, destination);

			AddToAttributeClusionCache(attribute, destination, result);

			return result;
		}
		

		private Boolean? CheckAttributeClusionCache([NotNull] T attribute, AttributeDestinations destination)
		{
			var cacheKey = GetAttributeClusionKey(attribute, destination);
			if (_cachedClusions.TryGetValue(cacheKey, out Boolean cachedClusion))
			{
				return cachedClusion;
			}

			return null;
		}

		private void AddToAttributeClusionCache([NotNull] T attribute, AttributeDestinations destination, Boolean result)
		{
			if (_cachedClusions.Count > MaxCacheSize)
				return;

			var cacheKey = GetAttributeClusionKey(attribute, destination);
			_cachedClusions[cacheKey] = result;
		}
		
		[NotNull]
		private static String GetAttributeClusionKey([NotNull] T attribute, AttributeDestinations destination)
		{
			// Enum is cast to INT to avoid enum.ToString which does reflection 
			// Since its only used as a key converting to INT should be fine.  

			// The cache key includes both the intended destinations of the attribute (attribute.DefaultDestinations) 
			// and the destination being tested.  This is because some attributes have the same name (like timestamp)
			// but different destinations.  Without this cache key, the wrong value will be selected.
			return attribute.Key + ((int)destination).ToString() + '_' + ((int)attribute.DefaultDestinations).ToString();
		}

		private Boolean ShouldExcludeAttribute([NotNull] T attribute, AttributeDestinations destination)
		{
			var explicitClusion = CheckForExplicitClusion(attribute, destination);
			if (explicitClusion != Clude.Unknown)
				return KnownCludeToBoolean(explicitClusion);

			var implicitClusion = CheckForImplicitClusion(attribute, destination);
			if (implicitClusion != Clude.Unknown)
				return KnownCludeToBoolean(implicitClusion);

			return false;
		}

		private Clude CheckForExplicitClusion([NotNull] T attribute, AttributeDestinations destination)
		{
			return _explicitAttributeTrie.GetClusion(attribute, destination);
		}

		private Clude CheckForImplicitClusion([NotNull] T attribute, AttributeDestinations destination)
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

		[NotNull]
		private static TrieNode<AttributeFilterNode> CreateAttributeNodeTrie([NotNull] IEnumerable<AttributeFilterNode> nodes)
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

		[NotNull]
		private static IEnumerable<AttributeFilterNode> CreateExplicitAttributeNodes([NotNull] Settings settings)
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

		[NotNull]
		private static IEnumerable<AttributeFilterNode> CreateImplicitAttributeNodes([NotNull] Settings settings)
		{
			var globalIncludes = CreateAttributeNodes(settings.ImplicitIncludes, AttributeDestinations.All, true);
			var globalExcludes = CreateAttributeNodes(settings.ImplicitExcludes, AttributeDestinations.All, false);

			return globalIncludes.Concat(globalExcludes);
		}

		[NotNull]
		private static IEnumerable<AttributeFilterNode> CreateAttributeNodes([NotNull] IEnumerable<String> keys, AttributeDestinations destinations, Boolean include)
		{
			return keys
				.Where(key => key != null)
				.Select(key => CreateAttributeNode(key, destinations, include));
		}

		[NotNull]
		private static AttributeFilterNode CreateAttributeNode([NotNull] String key, AttributeDestinations destinations, Boolean include)
		{
			var includes = (include) ? destinations : AttributeDestinations.None;
			var excludes = (include) ? AttributeDestinations.None : destinations;
			return new AttributeFilterNode(key, includes, excludes);
		}

		[NotNull]
		private static AttributeFilterNode MergeAttributeNodes([NotNull] IEnumerable<AttributeFilterNode> nodeBuilders)
		{
			var mergedNode = nodeBuilders.Aggregate(MergeAttributeNodes);
			if (mergedNode == null)
				throw new NullReferenceException("Attempt to merge attribute nodes yielded a null result.");
			return mergedNode;
		}

		private static AttributeFilterNode MergeAttributeNodes([NotNull] AttributeFilterNode left, [NotNull] AttributeFilterNode right)
		{
			var key = (left.Wildcard) ? left.Key + "*" : left.Key;
			var includes = left.DestinationIncludes | right.DestinationIncludes;
			var excludes = left.DestinationExcludes | right.DestinationExcludes;
			return new AttributeFilterNode(key, includes, excludes);
		}

		private static Int32 CompareAttributeNodes([NotNull] AttributeFilterNode left, [NotNull] AttributeFilterNode right)
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

		private static Int32 HashAttributeNode([NotNull] AttributeFilterNode nodeBuilder)
		{
			var suffix = (nodeBuilder.Wildcard) ? "*" : "";
			var stringToHash = nodeBuilder.Key + suffix;
			return stringToHash.GetHashCode();
		}

		private static Boolean CanParentAcceptChild([NotNull] AttributeFilterNode parent, [NotNull] AttributeFilterNode orphan)
		{
			if (!parent.Wildcard)
				return false;

			if (!orphan.Key.StartsWith(parent.Key))
				return false;

			return true;
		}

		private static Boolean CanNodeHaveChildren([NotNull] AttributeFilterNode node)
		{
			return node.Wildcard;
		}

		public class Settings
		{
			public Boolean AttributesEnabled = true;
			[NotNull]
			public IEnumerable<String> Excludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> Includes = Enumerable.Empty<String>();

			public Boolean JavaScriptAgentEnabled = true;
			[NotNull]
			public IEnumerable<String> JavaScriptAgentExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> JavaScriptAgentIncludes = Enumerable.Empty<String>();

			public Boolean ErrorTraceEnabled = true;
			[NotNull]
			public IEnumerable<String> ErrorTraceExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> ErrorTraceIncludes = Enumerable.Empty<String>();

			public Boolean TransactionEventEnabled = true;
			[NotNull]
			public IEnumerable<String> TransactionEventExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> TransactionEventIncludes = Enumerable.Empty<String>();

			public Boolean TransactionTraceEnabled = true;
			[NotNull]
			public IEnumerable<String> TransactionTraceExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> TransactionTraceIncludes = Enumerable.Empty<String>();

			[NotNull]
			public IEnumerable<String> ImplicitExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> ImplicitIncludes = Enumerable.Empty<String>();

			[NotNull]
			public IEnumerable<String> ErrorEventExcludes = Enumerable.Empty<String>();
			[NotNull]
			public IEnumerable<String> ErrorEventIncludes = Enumerable.Empty<String>();
			public Boolean ErrorEventsEnabled = true;

		}
	}
}
