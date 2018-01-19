using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace NewRelic.Trie
{
	public class TrieBuilder<T>
	{
		[NotNull]
		private readonly Func<T> _rootNodeMetaDataFactory;

		[NotNull]
		private readonly Func<IEnumerable<T>, T> _nodeMerger;

		[NotNull]
		private readonly Func<T, bool> _canNodeHaveChildrenChecker;

		[NotNull]
		private readonly TrieNodeComparor<T> _nodeComparor;

		public TrieBuilder(
			[NotNull] Func<T> rootNodeDataFactory,
			[NotNull] Func<IEnumerable<T>, T> nodeDataMerger,
			[NotNull] Func<T, T, Int32> nodeDataComparor,
			[NotNull] Func<T, Int32> nodeDataHasher,
			[NotNull] Func<T, T, Boolean> canParentAcceptChildChecker,
			[NotNull] Func<T, Boolean> canNodeHaveChildrenChecker)
		{
			_rootNodeMetaDataFactory = rootNodeDataFactory;
			_nodeMerger = nodeDataMerger;
			_canNodeHaveChildrenChecker = canNodeHaveChildrenChecker;
			_nodeComparor = new TrieNodeComparor<T>(nodeDataComparor, nodeDataHasher, canParentAcceptChildChecker);
		}

		[NotNull]
		public TrieNode<T> CreateTrie([NotNull] IEnumerable<T> nodeMetaDatas)
		{
			var rootNodeMetaData = _rootNodeMetaDataFactory();
			if (rootNodeMetaData == null)
				throw new NullReferenceException("Root node factory cannot return a null root node.");
			if (!_canNodeHaveChildrenChecker(rootNodeMetaData))
				throw new InvalidOperationException("Node returned by root node must be able to have children.");

			var rootNode = new TrieNode<T>(rootNodeMetaData);

			var datas = nodeMetaDatas
				.OrderBy(nodeMetaData => nodeMetaData, _nodeComparor)
				.GroupBy(nodeMetaData => nodeMetaData, _nodeComparor)
				.Where(NodeDataGroupingIsNotEmpty)
				.Select(MergeNodeDatas)
				.Select(TrieNodeFromData);

			foreach(var orphan in datas)
			{
				TryAddNodeAsChild(rootNode, orphan);
			}
			
			return rootNode;
		}

		[NotNull]
		private static TrieNode<T> TrieNodeFromData([NotNull] T metaData)
		{
			return new TrieNode<T>(metaData);
		}

		private static Boolean NodeDataGroupingIsNotEmpty([CanBeNull] IGrouping<T, T> nodes)
		{
			if (nodes == null)
				return false;

			return nodes.Any();
		}

		[NotNull]
		private T MergeNodeDatas([NotNull] IGrouping<T, T> nodes)
		{
			var mergedNode = _nodeMerger(nodes);
			if (mergedNode == null)
				throw new NullReferenceException("The node data merger method must return a non-null object.");
			return mergedNode;
		}

		private Boolean TryAddNodeAsChild([NotNull] TrieNode<T> parent, [NotNull] TrieNode<T> orphan)
		{
			if (!_nodeComparor.PotentialChild(parent.Data, orphan.Data))
				return false;

			if (!_canNodeHaveChildrenChecker(parent.Data))
				return false;

			var addedToChild = parent.Children
				.Where(child => child != null)
				.Where(child => TryAddNodeAsChild(child, orphan))
				.Any();
			if (addedToChild)
				return true;

			parent.Children.Add(orphan);
			return true;
		}
	}
}
