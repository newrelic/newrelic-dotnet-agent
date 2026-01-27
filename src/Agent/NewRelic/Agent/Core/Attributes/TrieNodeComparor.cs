// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;

namespace NewRelic.Agent.Core.Attributes;

internal class TrieNodeComparor<T> : IComparer<T>, IEqualityComparer<T>
{
    private readonly Func<T, T, int> _nodeComparor;

    private readonly Func<T, int> _nodeHasher;

    private readonly Func<T, T, bool> _potentialChildChecker;

    public int Compare(T left, T right)
    {
        if (left == null && right == null)
            return 0;

        if (left == null)
            return -1;

        if (right == null)
            return 1;

        return _nodeComparor(left, right);
    }

    public TrieNodeComparor(Func<T, T, int> nodeComparor, Func<T, int> nodeHasher, Func<T, T, bool> potentialChildChecker)
    {
        _nodeComparor = nodeComparor;
        _nodeHasher = nodeHasher;
        _potentialChildChecker = potentialChildChecker;
    }

    public bool Equals(T left, T right)
    {
        return Compare(left, right) == 0;
    }

    public int GetHashCode(T node)
    {
        return _nodeHasher(node);
    }

    public bool PotentialChild(T parent, T child)
    {
        return _potentialChildChecker(parent, child);
    }
}