// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;




namespace NewRelic.Collections.UnitTests
{
    public class Class_ConcurrentHashSet
    {
        private readonly ConcurrentHashSet<int> _concurrentHashSet;

        public Class_ConcurrentHashSet()
        {
            _concurrentHashSet = new ConcurrentHashSet<int>();
        }


        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 1 })]
        [TestCase(new[] { 1, 1, 2 })]
        public void ConcurrentHashSet_FunctionsAsNormalHashSet_ForSingleThreadedAccess(params int[] numbersToAdd)
        {
            // Because we're not doing anything interesting with the hashset itself, it seems reasonable to just wrap all of the basic hashset API tests into one test
            var distinctNumbers = numbersToAdd.Distinct().ToList();

            // Add
            foreach (var number in numbersToAdd)
                _concurrentHashSet.Add(number);

            // GetEnumerator<T>
            var index = 0;
            var genericEnumerator = _concurrentHashSet.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
                Assert.AreEqual(distinctNumbers[index++], genericEnumerator.Current);
            Assert.AreEqual(distinctNumbers.Count, index);

            // GetEnumerator
            index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentHashSet).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.AreEqual(distinctNumbers[index++], nongenericEnumerator.Current);
            Assert.AreEqual(distinctNumbers.Count, index);

            // Count
            Assert.AreEqual(_concurrentHashSet.Count, distinctNumbers.Count);

            // CopyTo
            var destinationArray = new int[distinctNumbers.Count];
            _concurrentHashSet.CopyTo(destinationArray, 0);
            Assert.True(distinctNumbers.SequenceEqual(destinationArray));

            // Contains
            Assert.True(distinctNumbers.All(_concurrentHashSet.Contains));

            // Remove
            _concurrentHashSet.Remove(distinctNumbers.First());
            Assert.False(_concurrentHashSet.Contains(distinctNumbers.First()));

            // Clear
            _concurrentHashSet.Clear();
            Assert.AreEqual(0, _concurrentHashSet.Count);
            Assert.False(distinctNumbers.Any(_concurrentHashSet.Contains));
        }

        [Test]
        public void ConcurrentHashSet_IsThreadSafe()
        {
            // Note: this test does not definitively prove that the collection is thread-safe,
            // but any of thread-safety test is better than no thread safety test.
            var random = new Random();

            var tasks = Enumerable.Range(1, 100)
                .Select(_ =>
                {
                    var numbersToAdd = new[] { random.Next(), random.Next(), random.Next() };
                    Action testAction = () => ExerciseFullApi(_concurrentHashSet, numbersToAdd);
                    return new Task(testAction);
                })
                .ToList();

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
        }

        private static void ExerciseFullApi(ConcurrentHashSet<int> hashSet, int[] numbersToAdd)
        {
            dynamic _;

            foreach (var number in numbersToAdd)
                hashSet.Add(number);

            var index = 0;
            var genericEnumerator = hashSet.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
            {
                _ = genericEnumerator.Current;
            }

            index = 0;
            var nongenericEnumerator = ((IEnumerable)hashSet).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
            {
                _ = nongenericEnumerator.Current;
            }

            _ = hashSet.Count;
            var destinationArray = new int[500];
            hashSet.CopyTo(destinationArray, 0);
            _ = hashSet.Contains(numbersToAdd.First());
            hashSet.Remove(numbersToAdd.First());
            hashSet.Clear();
        }
    }
}
