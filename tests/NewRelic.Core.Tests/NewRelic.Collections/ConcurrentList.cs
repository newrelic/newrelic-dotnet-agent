// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;




namespace NewRelic.Collections.UnitTests
{
    public class Class_ConcurrentList
    {
        private readonly ConcurrentList<int> _concurrentList;

        public Class_ConcurrentList()
        {
            _concurrentList = new ConcurrentList<int>();
        }


        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 1 })]
        [TestCase(new[] { 1, 1, 2 })]
        public void ConcurrentList_FunctionsAsNormalList_ForSingleThreadedAccess(params int[] numbersToAdd)
        {
            // Because we're not doing anything interesting with the list itself, it seems reasonable to just wrap all of the basic list API tests into one test

            // Add
            foreach (var number in numbersToAdd)
                _concurrentList.Add(number);

            // GetEnumerator<T>
            var index = 0;
            var genericEnumerator = _concurrentList.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
                Assert.That(genericEnumerator.Current, Is.EqualTo(numbersToAdd[index++]));
            Assert.That(index, Is.EqualTo(numbersToAdd.Length));

            // GetEnumerator
            index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentList).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.That(nongenericEnumerator.Current, Is.EqualTo(numbersToAdd[index++]));
            Assert.Multiple(() =>
            {
                Assert.That(index, Is.EqualTo(numbersToAdd.Length));

                // Count
                Assert.That(numbersToAdd, Has.Length.EqualTo(_concurrentList.Count));
            });

            // CopyTo
            var destinationArray = new int[numbersToAdd.Length];
            _concurrentList.CopyTo(destinationArray, 0);
            Assert.Multiple(() =>
            {
                Assert.That(numbersToAdd.SequenceEqual(destinationArray), Is.True);

                // Contains
                Assert.That(numbersToAdd.All(_concurrentList.Contains), Is.True);
            });

            // Remove
            _concurrentList.Remove(numbersToAdd.First());
            Assert.That(_concurrentList.SequenceEqual(numbersToAdd.Skip(1)), Is.True);

            // Insert
            _concurrentList.Insert(0, numbersToAdd.First());
            Assert.That(_concurrentList.SequenceEqual(numbersToAdd), Is.True);

            // IndexOf
            index = _concurrentList.IndexOf(numbersToAdd.First());
            Assert.That(index, Is.EqualTo(0));

            // RemoveAt
            _concurrentList.RemoveAt(0);
            Assert.That(_concurrentList.SequenceEqual(numbersToAdd.Skip(1)), Is.True);

            // Indexer -- Set
            _concurrentList.Insert(0, numbersToAdd.First());
            _concurrentList[0] = _concurrentList[0] + 1;
            Assert.Multiple(() =>
            {
                Assert.That(_concurrentList.First(), Is.EqualTo(numbersToAdd.First() + 1));

                // Indexer -- Get
                Assert.That(_concurrentList[0], Is.EqualTo(numbersToAdd.First() + 1));
            });

            // Clear
            _concurrentList.Clear();
            Assert.Multiple(() =>
            {
                Assert.That(_concurrentList, Is.Empty);
                Assert.That(numbersToAdd.Any(_concurrentList.Contains), Is.False);
            });
        }

        [Test]
        public void ConcurrentList_IsThreadSafe()
        {
            // Note: this test does not definitively prove that the collection is thread-safe,
            // but any thread-safety test is better than no thread safety test.
            var random = new Random();

            var tasks = Enumerable.Range(1, 100)
                .Select(_ =>
                {
                    var numbersToAdd = new[] { random.Next(), random.Next(), random.Next() };
                    Action testAction = () => ExerciseFullApi(_concurrentList, numbersToAdd);
                    return new Task(testAction);
                })
                .ToList();

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
        }

        private static void ExerciseFullApi(IList<int> concurrentList, int[] numbersToAdd)
        {
            dynamic _;

            // Add
            foreach (var number in numbersToAdd)
                concurrentList.Add(number);

            var index = 0;
            var genericEnumerator = concurrentList.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
            {
                _ = genericEnumerator.Current;
            }

            index = 0;
            var nongenericEnumerator = ((IEnumerable)concurrentList).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
            {
                _ = nongenericEnumerator.Current;
            }

            _ = concurrentList.Count;

            var destinationArray = new int[500];
            concurrentList.CopyTo(destinationArray, 0);
            _ = concurrentList.Contains(numbersToAdd.First());
            concurrentList.Remove(numbersToAdd.First());
            concurrentList.Insert(0, numbersToAdd.First());
            _ = concurrentList.IndexOf(numbersToAdd.First());

            try
            {
                // This operation can throw if another thread clears the collection
                concurrentList.RemoveAt(0);
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            concurrentList.Insert(0, numbersToAdd.First());
            try
            {
                // This operation can throw if another thread clears the collection
                concurrentList[0] = concurrentList[0] + 1;
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            try
            {
                // This operation can throw if another thread clears the collection
                _ = concurrentList[0];
            }
            catch (ArgumentOutOfRangeException)
            {
            }

            concurrentList.Clear();
        }
    }
}
