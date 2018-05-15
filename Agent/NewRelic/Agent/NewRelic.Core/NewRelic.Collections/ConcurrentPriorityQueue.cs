using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace NewRelic.Collections
{


	public class ConcurrentPriorityQueue<T> : IResizableCappedCollection<T>
	{
		private static object _syncroot = new object();

		private ulong _addCount = 0;

		private SortedSet<T> _sortedSet;

		public uint Size {  get; private set;  }

		public int Count { get { lock(_syncroot) return _sortedSet.Count; } }

		public bool IsReadOnly { get { return false; } }

		#region Constructors
		public ConcurrentPriorityQueue(uint capacity)
		{
			_sortedSet = new SortedSet<T>();
			Size = capacity;
		}

		public ConcurrentPriorityQueue(uint capacity, IComparer<T> comparer)
		{
			_sortedSet = new SortedSet<T>(comparer);
			Size = capacity;
		}
		#endregion Constructors

		public bool Add(T item)
		{
			bool addSucceeded = false;
			if (Size > 0)
			{
				lock (_syncroot)
				{
					++_addCount;
					addSucceeded = _sortedSet.Add(item);
					RemoveItemsFromBottomOfSortedSet();
				}
			}
			return addSucceeded;
		}
		void ICollection<T>.Add(T item)
		{
			Add(item);
		}

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			List<T> duplicate;
			//SortedSet.GetEnumerator duplicates the set into the enumerator, so lock to prevent race while they duplicate
			lock (_syncroot)
			{
				duplicate = _sortedSet.ToList();
			}
			return duplicate.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			List<T> duplicate;
			//SortedSet.GetEnumerator duplicates the set into the enumerator, so lock to prevent race while they duplicate
			lock (_syncroot)
			{
				duplicate = _sortedSet.ToList();
			}
			return duplicate.GetEnumerator();
		}

		//TODO: Amortize the pruning... E.g. allow the set to grow beyond Size by nnn nodes and then prune...
		private void RemoveItemsFromBottomOfSortedSet()
		{
			if (Size == 0)
			{
				Clear();
			}
			if (_sortedSet.Count > Size)
			{
				lock (_syncroot)
				{
					var setCount = (uint)_sortedSet.Count;
					if (setCount > Size)
					{
						var numberToRemove = setCount - Size;
						var reverseEnumerator = _sortedSet.Reverse().GetEnumerator();
						while (0 != numberToRemove--)
						{
							if (reverseEnumerator.MoveNext())
							{
								_sortedSet.Remove(reverseEnumerator.Current);
							}
						}
					}
				}
			}
		}

		public void Resize(uint newSize)
		{
			Size = newSize;
			RemoveItemsFromBottomOfSortedSet();
		}

		public ulong GetAddAttemptsCount()
		{
			return Volatile.Read(ref _addCount);
		}

		public void Clear()
		{
			lock (_syncroot)
			{
				_addCount = 0;
				_sortedSet.Clear();
			}
		}

		public bool Contains(T item)
		{
			var isContained = false; 
			lock (_syncroot)
			{
				isContained = _sortedSet.Contains(item);
				return isContained;
			}
		}

		public void CopyTo(T[] array, int arrayIndex)
		{
			lock (_syncroot)
			{
				_sortedSet.CopyTo(array, arrayIndex);
			}
		}

		public bool Remove(T item)
		{
			throw new NotSupportedException();
		}
	}
}