using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Core;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NewRelic.Collections.UnitTests
{
	public abstract class ConcurrentPriorityQueueTestsBase<T>  where T : IHasPriority
	{
		protected static int[] ConstructPriorityQueueSizes = { 0, 1, 10000, 20000 };

		protected readonly Dictionary<string, object> EmptyAttributes = new Dictionary<string, object>();
		protected const string TimeStampKey = "timestamp";

		protected ConcurrentPriorityQueue<PrioritizedNode<T>> ConcurrentPriorityQueue;

		protected int CreateCount;

		protected abstract PrioritizedNode<T> Create(float priority);

		protected void AllocPriorityQueue()
		{
			ConcurrentPriorityQueue = new ConcurrentPriorityQueue<PrioritizedNode<T>>(20);
		}

		public void FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			// Because nothing interesting happens when the reservoir's item count is below the size limit, it seems reasonable to just wrap all of the basic list API tests into one test
			var eventsToAdd = new[]
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
			var actualEvents = ConcurrentPriorityQueue.Select(node => node.Data).ToArray();
			var expectedEvents = eventsToAdd.Select(node => node.Data).ToArray();
			Assert.That(actualEvents, Is.EquivalentTo(expectedEvents));

			// Contains
			Assert.True(eventsToAdd.All(ConcurrentPriorityQueue.Contains));

			// Clear
			ConcurrentPriorityQueue.Clear();
			Assert.AreEqual(0, ConcurrentPriorityQueue.Count);
			Assert.False(eventsToAdd.Any(ConcurrentPriorityQueue.Contains));
		}

		public void ConstructPriorityQueueOfDifferentSizes(int sizeLimit)
		{
			var concurrentPriorityQueue = new ConcurrentPriorityQueue<PrioritizedNode<T>>(sizeLimit);
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
			const int numberOfItemsToAddInitially = 100;
			const int numberOfItemsToAddAfterReservoirLimitReached = 100;

			//Concurrent Priority Queue will only hold NumberOfItemsToAddInitially items.
			ConcurrentPriorityQueue.Resize(numberOfItemsToAddInitially);

			var itemsToAddInitially = new PrioritizedNode<T>[numberOfItemsToAddInitially];
			for (var i = 0; i < numberOfItemsToAddInitially; ++i)
			{
				itemsToAddInitially[i] = Create(i * 0.001f);
			}

			//fill the CPQ with values 100-199
			foreach (var itemToAdd in itemsToAddInitially)
			{
				Assert.IsTrue(ConcurrentPriorityQueue.Add(itemToAdd), "failed to add initial value");
			}

			//make sure they are all accounted for
			Assert.AreEqual(ConcurrentPriorityQueue.Count, numberOfItemsToAddInitially);

			//now add more items that will cause the items from above to get removed. these will be valued 0-99 (precisely 100 less than those above)
			for (var i = 0; i < numberOfItemsToAddAfterReservoirLimitReached; ++i)
			{
				var itemToAdd = Create((i + 100) * 0.001f);
				var itemThatWillGetRemoved = itemsToAddInitially[i];

				//each one we add will cause the corresponding smaller item to get removed.
				Assert.IsTrue(ConcurrentPriorityQueue.Add(itemToAdd), "failed to add subsequent value");
				Assert.IsTrue(ConcurrentPriorityQueue.Contains(itemToAdd), "added value not found");
				Assert.IsFalse(ConcurrentPriorityQueue.Contains(itemThatWillGetRemoved), "initial value did not get removed on addition of subsequent value");
				Assert.AreEqual(ConcurrentPriorityQueue.Count, numberOfItemsToAddInitially);
			}
		}

		public void SamplesItemsWhenSizeLimitReached_AddIEnumerable()
		{
			const int reservoirSize = 100;
			const int lowerPriority = 100;
			const int higherPriority = 300;
			const float priorityShift = 0.001f;

			//Concurrent Priority Queue will only hold NumberOfItemsToAddInitially items.
			ConcurrentPriorityQueue.Resize(reservoirSize);

			var higherPriorityItemsToAdd = new PrioritizedNode<T>[reservoirSize];
			var itemsToAddInitially = new PrioritizedNode<T>[reservoirSize];
			for (var i = 0; i < reservoirSize; ++i)
			{
				itemsToAddInitially[i] = Create((i + lowerPriority) * priorityShift);
				higherPriorityItemsToAdd[i] = Create((i + higherPriority) * priorityShift);
			}

			ConcurrentPriorityQueue.Add(itemsToAddInitially);

			//make sure they are all accounted for
			Assert.That(ConcurrentPriorityQueue.Count, Is.EqualTo(reservoirSize));

			ConcurrentPriorityQueue.Add(higherPriorityItemsToAdd);

			//make sure the size is not over 
			Assert.That(ConcurrentPriorityQueue.Count, Is.EqualTo(reservoirSize));
			
			foreach (var item in ConcurrentPriorityQueue)
			{
				Assert.That(item.Data.Priority, Is.GreaterThanOrEqualTo(higherPriority * priorityShift));
			}
			
		}

		public void IsThreadSafe()
		{
			// Note: this test does not definitively prove that the collection is thread-safe, but any thread-safety test is better than no thread safety test.
			var random = new Random();

			float GenPriority() { return (float)random.NextDouble(); }

			const int countOfThreads = 100;
			ConcurrentPriorityQueue.Resize(countOfThreads* 3);

			Assert.That(ConcurrentPriorityQueue.Size, Is.EqualTo(countOfThreads*3));

			var tasks = Enumerable.Range(1, countOfThreads)
				.Select(_ =>
				{
					var eventsToAdd = new[]
							{
							Create(GenPriority()),
							Create(GenPriority()),
							Create(GenPriority()),
							};
					void TestAction() => ExerciseFullApi(ConcurrentPriorityQueue, eventsToAdd, countOfThreads);
					return new Task(TestAction);
				})
				.ToList();

			// ReSharper disable PossibleNullReferenceException
			tasks.ForEach(task => task.Start());
			tasks.ForEach(task => task.Wait());
			// ReSharper restore PossibleNullReferenceException

			Assert.That(ConcurrentPriorityQueue, Has.Exactly(countOfThreads*3).Items);
		}

		// ReSharper disable RedundantAssignment
		private static void ExerciseFullApi(IResizableCappedCollection<PrioritizedNode<T>> concurrentPriorityQueue, PrioritizedNode<T>[] eventsToAdd, int countOfThreads)
		{
			// Add the new events
			foreach (var evt in eventsToAdd)
			{
				concurrentPriorityQueue.Add(evt);
			}

			//make sure each of the new events can be found (tests equality op)
			foreach (var ev in eventsToAdd)
			{
				Assert.That(concurrentPriorityQueue, Contains.Item(ev));
			}

			//copy the CPQ to an array
			var destinationArray = new PrioritizedNode<T>[eventsToAdd.Count() * countOfThreads];
			concurrentPriorityQueue.CopyTo(destinationArray, 0);

			//check that the copied contents contains our events
			foreach (var ev in eventsToAdd)
			{
				Assert.That(destinationArray, Contains.Item(ev));
			}

			//count how many are actually in the destinationArray
			var nonnullCount = 0;
			while (nonnullCount < destinationArray.Length && null != destinationArray[nonnullCount]) ++nonnullCount;

			//make sure the array is sorted properly
			for (var index = 1;  index < nonnullCount; ++index)
			{
				Assert.That(destinationArray[index - 1].Data.Priority, Is.GreaterThanOrEqualTo(destinationArray[index].Data.Priority));
			}

			//make sure that remove is not supported
			Assert.That(() => concurrentPriorityQueue.Remove(eventsToAdd[0]), Throws.TypeOf<NotSupportedException>());
		}
	}

	// ReSharper disable once InconsistentNaming
	[TestFixture]
	[TestOf(typeof(ConcurrentPriorityQueue<CustomEventWireModel>))]
	public class ConcurrentPriorityQueueCustomEventsTests : ConcurrentPriorityQueueTestsBase<CustomEventWireModel>
	{
		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override PrioritizedNode<CustomEventWireModel> Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			return new PrioritizedNode<CustomEventWireModel>(new CustomEventWireModel(priority, EmptyAttributes, EmptyAttributes));
		}

		[TestCaseSource("ConstructPriorityQueueSizes")]
		public void ConcurrentPriorityQueueCustomEventsTests_ConstructPQOfDifferentSizes(int sizeLimit)
		{
			ConstructPriorityQueueOfDifferentSizes(sizeLimit);
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
		public void ConcurrentPriorityQueueCustomEventsTests_SamplesItemsWhenSizeLimitReached_IEnumerable()
		{
			SamplesItemsWhenSizeLimitReached_AddIEnumerable();
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
		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override PrioritizedNode<ErrorEventWireModel> Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			var intrinsicAttributes = new Dictionary<string, object> { {TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds()} };
			return new PrioritizedNode<ErrorEventWireModel> (new ErrorEventWireModel(EmptyAttributes, intrinsicAttributes, EmptyAttributes, false, priority));
		}

		[TestCaseSource(nameof(ConstructPriorityQueueSizes))]
		public void ConcurrentPriorityQueueErrorEventsTests_ConstructPQOfDifferentSizes(int sizeLimit)
		{
			ConstructPriorityQueueOfDifferentSizes(sizeLimit);
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
		public void ConcurrentPriorityQueueErrorEventsTests_SamplesItemsWhenSizeLimitReached_IEnumerable()
		{
			SamplesItemsWhenSizeLimitReached_AddIEnumerable();
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
	public class
		ConcurrentPriorityQueueTransactionEventsTests : ConcurrentPriorityQueueTestsBase<TransactionEventWireModel>
	{
		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override PrioritizedNode<TransactionEventWireModel> Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			var intrinsicAttributes = new Dictionary<string, object> {{TimeStampKey, DateTime.UtcNow.ToUnixTimeMilliseconds()}};
			return new PrioritizedNode<TransactionEventWireModel> (new TransactionEventWireModel(EmptyAttributes, EmptyAttributes, intrinsicAttributes, false, priority));
		}

		[TestCaseSource("ConstructPriorityQueueSizes")]
		public void ConcurrentPriorityQueueTransactionEventsTests_ConstructPQOfDifferentSizes(int sizeLimit)
		{
			ConstructPriorityQueueOfDifferentSizes(sizeLimit);
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
		public void ConcurrentPriorityQueueTransactionEventsTests_SamplesItemsWhenSizeLimitReached_IEnumerable()
		{
			SamplesItemsWhenSizeLimitReached_AddIEnumerable();
		}

		[Test]
		public void ConcurrentPriorityQueueTransactionEventsTests_IsThreadSafe()
		{
			IsThreadSafe();
		}
	}

	[TestFixture]
	[TestOf(typeof(ConcurrentPriorityQueue<SpanEventWireModel>))]
	public class ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests : ConcurrentPriorityQueueTestsBase<SpanEventWireModel>
	{
		[SetUp]
		public void Setup()
		{
			AllocPriorityQueue();
		}

		protected override PrioritizedNode<SpanEventWireModel> Create(float priority)
		{
			Interlocked.Increment(ref CreateCount);
			var attributes = new Agent.Core.Attributes.AttributeCollection();
			attributes.Add(Agent.Core.Attributes.Attribute.BuildPriorityAttribute(priority));
			return new PrioritizedNode<SpanEventWireModel>(new SpanEventWireModel(priority, attributes.GetIntrinsicsDictionary(), attributes.GetUserAttributesDictionary(), attributes.GetAgentAttributesDictionary()));
		}

		[TestCaseSource("ConstructPriorityQueueSizes")]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_ConstructPQOfDifferentSizes(int sizeLimit)
		{
			ConstructPriorityQueueOfDifferentSizes(sizeLimit);
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_FunctionsAsNormalList_ForSingleThreadedAccess()
		{
			FunctionsAsNormalList_ForSingleThreadedAccess();
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_ResizeChangesMaximumItemsAllowed()
		{
			ResizeChangesMaximumItemsAllowed();
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_GetAddAttemptsCount()
		{
			GetAddAttemptsCount();
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_SamplesItemsWhenSizeLimitReached()
		{
			SamplesItemsWhenSizeLimitReached();
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_SamplesItemsWhenSizeLimitReached_IEnumerable()
		{
			SamplesItemsWhenSizeLimitReached_AddIEnumerable();
		}

		[Test]
		public void ConcurrentPriorityQueuePrioritizedNodeSpanEventsTests_IsThreadSafe()
		{
			IsThreadSafe();
		}

	}
}
