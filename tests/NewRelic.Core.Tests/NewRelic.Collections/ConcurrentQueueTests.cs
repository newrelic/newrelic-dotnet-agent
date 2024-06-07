// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;




namespace NewRelic.Collections.UnitTests
{
    public class ConcurrentQueueTests
    {
        private readonly ConcurrentQueue<int> _concurrentQueue;

        public ConcurrentQueueTests()
        {
            _concurrentQueue = new ConcurrentQueue<int>();
        }


        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 1 })]
        [TestCase(new[] { 1, 1, 2 })]
        public void ConcurrentQueue_FunctionsAsNormalQueue_ForSingleThreadedAccess(params int[] numbersToAdd)
        {
            // Because we're not doing anything interesting with the queue itself, it seems reasonable to just wrap all of the basic queue API tests into one test

            // Enqueue
            foreach (var number in numbersToAdd)
                _concurrentQueue.Enqueue(number);

            // Peek
            var head = _concurrentQueue.Peek();
            Assert.That(head, Is.EqualTo(numbersToAdd.First()));

            // GetEnumerator<T>
            var index = 0;
            var genericEnumerator = _concurrentQueue.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
                Assert.That(genericEnumerator.Current, Is.EqualTo(numbersToAdd[index++]));
            Assert.That(index, Is.EqualTo(numbersToAdd.Length));

            // GetEnumerator
            index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentQueue).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.That(nongenericEnumerator.Current, Is.EqualTo(numbersToAdd[index++]));
            Assert.Multiple(() =>
            {
                Assert.That(index, Is.EqualTo(numbersToAdd.Length));

                // Count
                Assert.That(numbersToAdd, Has.Length.EqualTo(_concurrentQueue.Count));
            });

            // CopyTo
            var destinationArray = new int[numbersToAdd.Length];
            _concurrentQueue.CopyTo(destinationArray, 0);
            Assert.Multiple(() =>
            {
                Assert.That(numbersToAdd.SequenceEqual(destinationArray), Is.True);

                // Contains
                Assert.That(numbersToAdd.All(_concurrentQueue.Contains), Is.True);
            });

            // Dequeue
            head = _concurrentQueue.Dequeue();
            Assert.Multiple(() =>
            {
                Assert.That(head, Is.EqualTo(numbersToAdd.First()));
                Assert.That(_concurrentQueue.SequenceEqual(numbersToAdd.Skip(1)), Is.True);
            });

            // Clear
            _concurrentQueue.Clear();
            Assert.Multiple(() =>
            {
                Assert.That(_concurrentQueue, Is.Empty);
                Assert.That(numbersToAdd.Any(_concurrentQueue.Contains), Is.False);
            });
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
                    var numbersToAdd = new[] { random.Next(), random.Next(), random.Next() };
                    Action testAction = () => ExerciseFullApi(_concurrentQueue, numbersToAdd);
                    return new Task(testAction);
                })
                .ToList();

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
        }

        private static void ExerciseFullApi(ConcurrentQueue<int> concurrentQueue, int[] numbersToAdd)
        {
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

            var destinationArray = new int[500];
            concurrentQueue.CopyTo(destinationArray, 0);
            _ = concurrentQueue.Contains(numbersToAdd.First());
            _ = concurrentQueue.DequeueOrDefault();
            concurrentQueue.Clear();
        }
    }
}
