// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Agent.Extensions.Collections
{
    public class PartitionedBlockingCollection<T>
    {
        private BlockingCollection<T>[] _collections;

        public readonly int Capacity;

        private int _count;
        public int Count => _count;

        public int PartitionCount => _collections.Length;

        public PartitionedBlockingCollection(int capacity, int partitionCount)
        {
            Capacity = capacity;
            var collections = new List<BlockingCollection<T>>();
            var proposedPartitionSize = Convert.ToInt32(Math.Ceiling(Convert.ToSingle(capacity) / partitionCount));
            
            for (var i = 0; i < partitionCount; i++)
            {
                var actualPartitionSize = Math.Min(capacity, proposedPartitionSize);
                collections.Add(new BlockingCollection<T>(actualPartitionSize));

                capacity = capacity - actualPartitionSize;

                if (capacity <= 0)
                {
                    break;
                }
            }

            _collections = collections.ToArray();
        }

        public PartitionedBlockingCollection(int capacity, int partitionCount, PartitionedBlockingCollection<T> fromCollection)
            :this(capacity,partitionCount)
        {
            while (fromCollection.TryTake(out var item)  && TryAdd(item))
#pragma warning disable S108 // Nested blocks of code should not be left empty
            {
            }
#pragma warning restore S108 // Nested blocks of code should not be left empty

        }

        public PartitionedBlockingCollection(int capacity, int partitionCount, IEnumerable<T> items)
            : this(capacity, partitionCount)
        {
            TryAdd(items);
        }

        public bool TryAdd(T item)
        {
            if(BlockingCollection<T>.TryAddToAny(_collections, item) != -1)
            {
                Interlocked.Increment(ref _count);
                return true;
            }

            return false;
        }

        public int TryAdd(IEnumerable<T> items)
        {
            var count = 0;
            foreach (var item in items)
            {
                if (!TryAdd(item))
                {
                    return count;
                }

                count++;
            }

            return count;
        }

        public bool TryTake(out T item)
        {
            if(BlockingCollection<T>.TryTakeFromAny(_collections, out item) != -1)
            {
                Interlocked.Decrement(ref _count);
                return true;
            }

            return false;
        }


        public bool Take(out T item, CancellationToken token)
        {
            if(BlockingCollection<T>.TakeFromAny(_collections, out item, token) != -1)
            {
                Interlocked.Decrement(ref _count);
                return true;
            }

            return false;
        }

        public List<T> ToList()
        {
            return _collections.SelectMany(x => x).ToList();
        }
    }
}
