// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Extensions.Collections
{

    public class PrioritizedNode<T> : IComparable, IEquatable<PrioritizedNode<T>>, IComparable<PrioritizedNode<T>> where T : IHasPriority
    {
        public T Data { get; }

        private readonly float _priority;
        private readonly long _count;
        private readonly int _hashCode;
        public override int GetHashCode() => _hashCode;

        public PrioritizedNode(T data)
        {
            Data = data;
            _priority = data.Priority;
            _count = StaticCounter.Next();

            var hashCode = 1038332868;
            hashCode = hashCode * -1521134295 + _priority.GetHashCode();
            hashCode = hashCode * -1521134295 + _count.GetHashCode();
            _hashCode = hashCode;
        }

        public int CompareTo(object otherObject)
        {
            switch (otherObject)
            {
                case null:
                    return 1;
                case PrioritizedNode<T> node:
                    return CompareTo(node);
            }

            throw new ArgumentException($"must be {nameof(PrioritizedNode<T>)}");
        }

        public int CompareTo(PrioritizedNode<T> otherNode)
        {
            if (otherNode is null)
            {
                return 1;
            }

            //note: we flip the sign of the priorityResult to sort descending (larger values come first)
            var priorityResult = _priority.CompareTo(otherNode._priority);
            return priorityResult != 0 ? -priorityResult : _count.CompareTo(otherNode._count);
        }

        public static bool operator <=(PrioritizedNode<T> lhsNode, PrioritizedNode<T> rhsNode)
        {
            if (lhsNode is null)
            {
                throw new ArgumentNullException(nameof(lhsNode));
            }

            return lhsNode.CompareTo(rhsNode) <= 0;
        }

        public static bool operator <(PrioritizedNode<T> lhsNode, PrioritizedNode<T> rhsNode)
        {
            if (lhsNode is null)
            {
                throw new ArgumentNullException(nameof(lhsNode));
            }

            return lhsNode.CompareTo(rhsNode) < 0;
        }

        public static bool operator >=(PrioritizedNode<T> operand1, PrioritizedNode<T> rhsNode)
        {
            return rhsNode <= operand1;
        }

        public static bool operator >(PrioritizedNode<T> lhsNode, PrioritizedNode<T> rhsNode)
        {
            return rhsNode < lhsNode;
        }

        public override bool Equals(object otherObject)
        {
            return (otherObject is PrioritizedNode<T> otherNode) && Equals(otherNode);
        }

        public bool Equals(PrioritizedNode<T> otherNode)
        {
            return ReferenceEquals(this, otherNode) ||
                   (!(otherNode is null) && _priority.Equals(otherNode._priority) && _count.Equals(otherNode._count));
        }

        public static bool operator ==(PrioritizedNode<T> lhsNode, PrioritizedNode<T> rhsNode)
        {
            //return true only if a and b are null or a is not null and Equals() returns true
            return lhsNode?.Equals(rhsNode) ?? rhsNode is null;
        }

        public static bool operator !=(PrioritizedNode<T> lhsNode, PrioritizedNode<T> rhsNode)
        {
            return !(lhsNode == rhsNode);
        }

        public override string ToString()
        {
            return $"{_priority:F6}-{_count}";
        }
    }
}
