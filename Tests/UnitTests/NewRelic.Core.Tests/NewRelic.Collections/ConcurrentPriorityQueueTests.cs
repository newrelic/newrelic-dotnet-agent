using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NUnit.Framework;
using NewRelic.Agent.Core.WireModels;
using System.Collections.Generic;
using System.Threading;
using NewRelic.Agent.Core.Utilities;

namespace NewRelic.Collections.UnitTests
{
	public abstract class ConcurrentPriorityQueueTestsBase<T>
	{
		protected static uint[] ConstructPQSizes = new uint[] { 0u, 1u, 10000u, 20000u };

		protected readonly static Dictionary<string, object> EmptyAttributes = new Dictionary<string, object>();
		protected const string TimeStampKey = "timestamp";

		[NotNull]
		protected ConcurrentPriorityQueue<T> ConcurrentPriorityQueue;

		protected int CreateCount = 0;

		protected abstract T Create(float priority);

		protected IComparer<T> Comparer { get; set; }

		protected void AllocPriorityQueue()
		{
			ConcurrentPriorityQueue = new ConcurrentPriorityQueue<T>(20, Comparer);
		}

		public void FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			// Because nothing interesting happens when the reservoir's item count is below the size limit, it seems reasonable to just wrap all of the basic list API tests into one test
			var eventsToAdd = new T[]
			{
					Create(0.3f),
					Create(0.2f),
					Create(0.1f),
			};

			// Add
			foreach (var ev in eventsToAdd)
			{
				ConcurrentPriorityQueue.Add(ev);
			}

			// GetEnumerator
			var index = 0;
			var nongenericEnumerator = ((IEnumerable)ConcurrentPriorityQueue).GetEnumerator();
			while (index < eventsToAdd.Length && nongenericEnumerator.MoveNext())
			{
				Assert.AreEqual(eventsToAdd[index++], nongenericEnumerator.Current);
			}
			Assert.AreEqual(eventsToAdd.Length, index);

			// Count
			Assert.AreEqual(ConcurrentPriorityQueue.Count, eventsToAdd.Length);

			// CopyTo
			var destinationArray = new T[eventsToAdd.Length];
			ConcurrentPriorityQueue.CopyTo(destinationArray, 0);
			Assert.True(eventsToAdd.SequenceEqual(destinationArray));

			// Contains
			Assert.True(eventsToAdd.All(ConcurrentPriorityQueue.Contains));

			// Clear
			ConcurrentPriorityQueue.Clear();
			Assert.AreEqual(0, ConcurrentPriorityQueue.Count);
			Assert.False(eventsToAdd.Any(ConcurrentPriorityQueue.Contains));
		}

		public void ConstructPQOfDifferentSizes(uint sizeLimit)
		{
			var concurrentPriorityQueue = new ConcurrentPriorityQueue<T>(sizeLimit, Comparer);
			Assert.AreEqual(concurrentPriorityQueue.Size, sizeLimit);
		}

		public void ResizeChangesMaximumItemsAllowed()
		{
			// Resize
			ConcurrentPriorityQueue.Add(Create(0.1f));
			ConcurrentPriorityQueue.Add(Create(0.2f));
			ConcurrentPriorityQueue.Add(Create(0.3f));
			Assert.AreEqual(3, ConcurrentPriorityQueue.Count);
			ConcurrentPriorityQueue.Resize(2);
			Assert.AreEqual(2, ConcurrentPriorityQueue.Count);
			ConcurrentPriorityQueue.Add(Create(0.4f));
			Assert.AreEqual(2, ConcurrentPriorityQueue.Count);
		}

		public void GetAddAttemptsCount()
		{
			Assert.AreEqual((ulong)0, ConcurrentPriorityQueue.GetAddAttemptsCount());
			ConcurrentPriorityQueue.Add(Create(0.1f));
			ConcurrentPriorityQueue.Add(Create(0.2f));
			ConcurrentPriorityQueue.Add(Create(0.3f));
			Assert.AreEqual((ulong)3, ConcurrentPriorityQueue.GetAddAttemptsCount());
		}

		public void SamplesItemsWhenSizeLimitReached()
		{
			const int NumberOfItemsToAddInitially = 100;
			const int numberOfItemsToAddAfterReservoirLimitReached = 100;

			//Concurrent Priority Queue will only hold NumberOfItemsToAddInitially items.
			ConcurrentPriorityQueue.Resize(NumberOfItemsToAddInitially);

			var ItemsToAddInitially = new T[NumberOfItemsToAddInitially];
			for (var i = 0; i < NumberOfItemsToAddInitially; ++i)
			{
				ItemsToAddInitially[i] = Create(i * 0.001f);
			}

			//fill the CPQ with values 100-199
			foreach (var itemToAdd in ItemsToAddInitially)
			{
				Assert.IsTrue(ConcurrentPriorityQueue.Add(itemToAdd), "failed to add initial value", itemToAdd);
			}

			//make sure they are all accounted for
			Assert.AreEqual(ConcurrentPriorityQueue.Count, NumberOfItemsToAddInitially);

			//now add more items that will cause the items from above to get removed. these will be valued 0-99 (precisely 100 less than those above)
			for (var i = 0; i < numberOfItemsToAddAfterReservoirLimitReached; ++i)
			{
				var itemToAdd = Create((i + 100) * 0.001f);
				var itemThatWillGetRemoved = ItemsToAddInitially[i];

				//each one we add will cause the corresponding smaller item to get removed.
				Assert.IsTrue(ConcurrentPriorityQueue.Add(itemToAdd), "failed to add subsequent value", itemToAdd);
				Assert.IsTrue(ConcurrentPriorityQueue.Contains(itemToAdd), "added value not found", itemToAdd);
				Assert.IsFalse(ConcurrentPriorityQueue.Contains(itemThatWillGetRemoved), "initial value did not get removed on addition of subsequent value", itemThatWillGetRemoved, itemToAdd);
			}
		}

		public void IsThreadSafe()
		{
			// Note: this test does not definitively prove that the collection is thread-safe, but any thread-safety test is better than no thread safety test.
			var random = new Random();

			float GenPriority() { return (float)random.NextDouble(); };

			const int CountOfThreads = 100;
			var tasks = Enumerable.Range(1, CountOfThreads)
				.Select(_ =>
				{
					var eventsToAdd = new T[]
							{
							Create(GenPriority()),
							Create(GenPriority()),
							Create(GenPriority()),
							};
					void testAction() => ExerciseFullApi(ConcurrentPriorityQueue, eventsToAdd, CountOfThreads);
					return new Task(testAction);
				})
				.ToList();

			// ReSharper disable PossibleNullReferenceException
			tasks.ForEach(task => task.Start());
			tasks.ForEach(task => task.Wait());
			// ReSharper restore PossibleNullReferenceException
		}

		// ReSharper disable RedundantAssignment
		private static void ExerciseFullApi([NotNull] IResizableCappedCollection<T> concurrentPriorityQueue, [NotNull] T[] eventsToAdd, int countOfThreads)
		{
			// ReSharper disable once NotAccessedVariable
			dynamic _;

			// Add
			foreach (var evt in eventsToAdd)
			{
				concurrentPriorityQueue.Add(evt);
			}

			var index = 0;
			var genericEnumerator = concurrentPriorityQueue.GetEnumerator();
			while (index < eventsToAdd.Length && genericEnumerator.MoveNext())
			{
				_ = genericEnumerator.Current;
			}

			index = 0;
			var nongenericEnumerator = ((IEnumerable)concurrentPriorityQueue).GetEnumerator();
			while (index < eventsToAdd.Length && nongenericEnumerator.MoveNext())
			{
				_ = nongenericEnumerator.Current;
			}

			_ = concurrentPriorityQueue.Count;

			var destinationArray = new T[eventsToAdd.Count() * countOfThreads];
			concurrentPriorityQueue.CopyTo(destinationArray, 0);
			_ = concurrentPriorityQueue.Contains(eventsToAdd.First());

			try
			{
				concurrentPriorityQueue.Remove(eventsToAdd.First());
			}
			catch (NotSupportedException)
			{
			}

			concurrentPriorityQueue.Clear();
		}
		// ReSharper restore RedundantAssignment

	}

	// ReSharper disable once InconsistentNaming
	[TestFixture]
	[TestOf(typeof(ConcurrentPriorityQueue<CustomEventWireModel>))]
	public class ConcurrentPriorityQueueCustomEventsTests : ConcurrentPriorityQueueTestsBase<CustomEventWireModel>
	{
		public ConcurrentPriorityQueueCustomEventsTests()
		{
			Comparer = new CustomEventWireModel.PriorityTimestampComparer();
		}

		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override CustomEventWireModel Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			return new CustomEventWireModel("event type" + CreateCount.ToString(), DateTime.UtcNow, EmptyAttributes, priority);
		}

		[TestCaseSource("ConstructPQSizes")]
		public void ConcurrentPriorityQueueCustomEventsTests_ConstructPQOfDifferentSizes(uint sizeLimit)
		{
			ConstructPQOfDifferentSizes(sizeLimit);
		}

		[Test]
		public void ConcurrentPriorityQueueCustomEventsTests_FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			FunctionsAsNormalList_ForSingleThreadedAccess();
		}

		[Test]
		public void ConcurrentPriorityQueueCustomEventsTests_ResizeChangesMaximumItemsAllowed()
		{
			ResizeChangesMaximumItemsAllowed();
		}

		[Test]
		public void ConcurrentPriorityQueueCustomEventsTests_GetAddAttemptsCount()
		{
			GetAddAttemptsCount();
		}

		[Test]
		public void ConcurrentPriorityQueueCustomEventsTests_SamplesItemsWhenSizeLimitReached()
		{
			SamplesItemsWhenSizeLimitReached();
		}

		[Test]
		public void ConcurrentPriorityQueueCustomEventsTests_IsThreadSafe()
		{
			IsThreadSafe();
		}
	}


	// ReSharper disable once InconsistentNaming
	[TestFixture]
	[TestOf(typeof(ConcurrentPriorityQueue<ErrorEventWireModel>))]
	public class ConcurrentPriorityQueueErrorEventsTests: ConcurrentPriorityQueueTestsBase<ErrorEventWireModel>
	{
		public ConcurrentPriorityQueueErrorEventsTests()
		{
			Comparer = new ErrorEventWireModel.PriorityTimestampComparer();
		}

		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override ErrorEventWireModel Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			var intrinsicAttributes = new Dictionary<String, Object> { {TimeStampKey, DateTime.UtcNow.ToUnixTime()} };
			return new ErrorEventWireModel(EmptyAttributes, intrinsicAttributes, EmptyAttributes, false, priority);
		}

		[TestCaseSource("ConstructPQSizes")]
		public void ConcurrentPriorityQueueErrorEventsTests_ConstructPQOfDifferentSizes(uint sizeLimit)
		{
			ConstructPQOfDifferentSizes(sizeLimit);
		}

		[Test]
		public void ConcurrentPriorityQueueErrorEventsTests_FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			FunctionsAsNormalList_ForSingleThreadedAccess();
		}

		[Test]
		public void ConcurrentPriorityQueueErrorEventsTests_ResizeChangesMaximumItemsAllowed()
		{
			ResizeChangesMaximumItemsAllowed();
		}

		[Test]
		public void ConcurrentPriorityQueueErrorEventsTests_GetAddAttemptsCount()
		{
			GetAddAttemptsCount();
		}


		[Test]
		public void ConcurrentPriorityQueueErrorEventsTests_SamplesItemsWhenSizeLimitReached()
		{
			SamplesItemsWhenSizeLimitReached();
		}

		[Test]
		public void ConcurrentPriorityQueueErrorEventsTests_IsThreadSafe()
		{
			IsThreadSafe();
		}
	}

	// ReSharper disable once InconsistentNaming
	[TestFixture]
	[TestOf(typeof(ConcurrentPriorityQueue<TransactionEventWireModel>))]
	public class ConcurrentPriorityQueueTransactionEventsTests : ConcurrentPriorityQueueTestsBase<TransactionEventWireModel>
	{
		public ConcurrentPriorityQueueTransactionEventsTests()
		{
			Comparer = new TransactionEventWireModel.PriorityTimestampComparer();
		}

		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override TransactionEventWireModel Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			var intrinsicAttributes = new Dictionary<String, Object> { { TimeStampKey, DateTime.UtcNow.ToUnixTime() } };
			return new TransactionEventWireModel(EmptyAttributes, EmptyAttributes, intrinsicAttributes, false, priority);
		}

		[TestCaseSource("ConstructPQSizes")]
		public void ConcurrentPriorityQueueTransactionEventsTests_ConstructPQOfDifferentSizes(uint sizeLimit)
		{
			ConstructPQOfDifferentSizes(sizeLimit);
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			FunctionsAsNormalList_ForSingleThreadedAccess();
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_ResizeChangesMaximumItemsAllowed()
		{
			ResizeChangesMaximumItemsAllowed();
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_GetAddAttemptsCount()
		{
			GetAddAttemptsCount();
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_SamplesItemsWhenSizeLimitReached()
		{
			SamplesItemsWhenSizeLimitReached();
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_IsThreadSafe()
		{
			IsThreadSafe();
		}
	}
}
