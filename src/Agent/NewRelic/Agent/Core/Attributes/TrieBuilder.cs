// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;

namespace NewRelic.Agent.Core.Attributes;

public class TrieBuilder<T>
{
    private readonly Func<T> _rootNodeMetaDataFactory;

    private readonly Func<IEnumerable<T>, T> _nodeMerger;

    private readonly Func<T, bool> _canNodeHaveChildrenChecker;

    private readonly TrieNodeComparor<T> _nodeComparor;

    public TrieBuilder(
        Func<T> rootNodeDataFactory,
        Func<IEnumerable<T>, T> nodeDataMerger,
        Func<T, T, int> nodeDataComparor,
        Func<T, int> nodeDataHasher,
        Func<T, T, bool> canParentAcceptChildChecker,
        Func<T, bool> canNodeHaveChildrenChecker)
    {
        _rootNodeMetaDataFactory = rootNodeDataFactory;
        _nodeMerger = nodeDataMerger;
        _canNodeHaveChildrenChecker = canNodeHaveChildrenChecker;
        _nodeComparor = new TrieNodeComparor<T>(nodeDataComparor, nodeDataHasher, canParentAcceptChildChecker);
    }

    public TrieNode<T> CreateTrie(IEnumerable<T> nodeMetaDatas)
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

        foreach (var orphan in datas)
        {
            TryAddNodeAsChild(rootNode, orphan);
        }

        return rootNode;
    }

    private static TrieNode<T> TrieNodeFromData(T metaData)
    {
        return new TrieNode<T>(metaData);
    }

    private static bool NodeDataGroupingIsNotEmpty(IGrouping<T, T> nodes)
    {
        if (nodes == null)
            return false;

        return nodes.Any();
    }

    private T MergeNodeDatas(IGrouping<T, T> nodes)
    {
        var mergedNode = _nodeMerger(nodes);
        if (mergedNode == null)
            throw new NullReferenceException("The node data merger method must return a non-null object.");
        return mergedNode;
    }

    private bool TryAddNodeAsChild(TrieNode<T> parent, TrieNode<T> orphan)
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