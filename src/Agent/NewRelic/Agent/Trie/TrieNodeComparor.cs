using System;
using System.Collections.Generic;

namespace NewRelic.Trie
{
    internal class TrieNodeComparor<T> : IComparer<T>, IEqualityComparer<T>
    {
        private readonly Func<T, T, Int32> _nodeComparor;
        private readonly Func<T, Int32> _nodeHasher;
        private readonly Func<T, T, Boolean> _potentialChildChecker;

        public Int32 Compare(T left, T right)
        {
            if (left == null && right == null)
                return 0;

            if (left == null)
                return -1;

            if (right == null)
                return 1;

            return _nodeComparor(left, right);
        }

        public TrieNodeComparor(Func<T, T, Int32> nodeComparor, Func<T, Int32> nodeHasher, Func<T, T, Boolean> potentialChildChecker)
        {
            _nodeComparor = nodeComparor;
            _nodeHasher = nodeHasher;
            _potentialChildChecker = potentialChildChecker;
        }

        public Boolean Equals(T left, T right)
        {
            return Compare(left, right) == 0;
        }

        public Int32 GetHashCode(T node)
        {
            return _nodeHasher(node);
        }

        public Boolean PotentialChild(T parent, T child)
        {
            return _potentialChildChecker(parent, child);
        }
    }
}
