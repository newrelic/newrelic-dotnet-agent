﻿using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NUnit.Framework;




namespace NewRelic.Collections.UnitTests
{
	// ReSharper disable once InconsistentNaming
	public class ConcurrentQueueTests
	{
		[NotNull]
		private readonly ConcurrentQueue<Int32> _concurrentQueue;

		public ConcurrentQueueTests()
		{
			_concurrentQueue = new ConcurrentQueue<Int32>();
		}

		
		[TestCase(new[] {1})]
		[TestCase(new[] {1, 1})]
		[TestCase(new[] {1, 1, 2})]
		public void ConcurrentQueue_FunctionsAsNormalQueue_ForSingleThreadedAccess([NotNull] params Int32[] numbersToAdd)
		{
			// Because we're not doing anything interesting with the queue itself, it seems reasonable to just wrap all of the basic queue API tests into one test

			// Enqueue
			foreach (var number in numbersToAdd)
				_concurrentQueue.Enqueue(number);

			// Peek
			var head = _concurrentQueue.Peek();
			Assert.AreEqual(numbersToAdd.First(), head);

			// GetEnumerator<T>
			var index = 0;
			var genericEnumerator = _concurrentQueue.GetEnumerator();
			while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
				Assert.AreEqual(numbersToAdd[index++], genericEnumerator.Current);
			Assert.AreEqual(numbersToAdd.Length, index);

			// GetEnumerator
			index = 0;
			var nongenericEnumerator = ((IEnumerable)_concurrentQueue).GetEnumerator();
			while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
				Assert.AreEqual(numbersToAdd[index++], nongenericEnumerator.Current);
			Assert.AreEqual(numbersToAdd.Length, index);

			// Count
			Assert.AreEqual(_concurrentQueue.Count, numbersToAdd.Length);

			// CopyTo
			var destinationArray = new Int32[numbersToAdd.Length];
			_concurrentQueue.CopyTo(destinationArray, 0);
			Assert.True(numbersToAdd.SequenceEqual(destinationArray));

			// Contains
			Assert.True(numbersToAdd.All(_concurrentQueue.Contains));

			// Dequeue
			head = _concurrentQueue.Dequeue();
			Assert.AreEqual(numbersToAdd.First(), head);
			Assert.True(_concurrentQueue.SequenceEqual(numbersToAdd.Skip(1)));

			// Clear
			_concurrentQueue.Clear();
			Assert.AreEqual(0, _concurrentQueue.Count);
			Assert.False(numbersToAdd.Any(_concurrentQueue.Contains));
		}

		[Test]
		public void ConcurrentQueue_IsThreadSafe()
		{
			// Note: this test does not definitively prove that the collection is thread-safe,
			// but any thread-safety test is better than no thread safety test.
			var random = new Random();

			var tasks = Enumerable.Range(1, 100)
				.Select(_ =>
				{
					var numbersToAdd = new[] {random.Next(), random.Next(), random.Next()};
					Action testAction = () => ExerciseFullApi(_concurrentQueue, numbersToAdd);
					return new Task(testAction);
				})
				.ToList();

			// ReSharper disable PossibleNullReferenceException
			tasks.ForEach(task => task.Start());
			tasks.ForEach(task => task.Wait());
			// ReSharper restore PossibleNullReferenceException
		}

		// ReSharper disable RedundantAssignment
		private static void ExerciseFullApi([NotNull] ConcurrentQueue<Int32> concurrentQueue, [NotNull] Int32[] numbersToAdd)
		{
			// ReSharper disable once NotAccessedVariable
			dynamic _;

			// Enqueue
			foreach (var number in numbersToAdd)
				concurrentQueue.Enqueue(number);

			// Peek
			try
			{
				_ = concurrentQueue.Peek();
			}
			catch (InvalidOperationException)
			{
			}

			var index = 0;
			var genericEnumerator = concurrentQueue.GetEnumerator();
			while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
			{
				_ = genericEnumerator.Current;
			}

			index = 0;
			var nongenericEnumerator = ((IEnumerable)concurrentQueue).GetEnumerator();
			while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
			{
				_ = nongenericEnumerator.Current;
			}

			_ = concurrentQueue.Count;

			var destinationArray = new Int32[500];
			concurrentQueue.CopyTo(destinationArray, 0);
			_ = concurrentQueue.Contains(numbersToAdd.First());
			_ = concurrentQueue.DequeueOrDefault();
			concurrentQueue.Clear();
		}
		// ReSharper restore RedundantAssignment
	}
}
