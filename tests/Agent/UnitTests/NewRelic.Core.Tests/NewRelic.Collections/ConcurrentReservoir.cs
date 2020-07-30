/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace NewRelic.Collections.UnitTests
{
    public class ConcurrentReservoirTests
    {
        private ConcurrentReservoir<int> _concurrentReservoir;

        public ConcurrentReservoirTests()
        {
            _concurrentReservoir = new ConcurrentReservoir<int>(20);
        }

        [SetUp]
        public void Setup()
        {
            _concurrentReservoir = new ConcurrentReservoir<int>(20);
        }

        [TestCase(0u)]
        [TestCase(1u)]
        [TestCase(10000u)]
        [TestCase(20000u)]
        public void concurrentReservoir_Constructor(uint sizeLimit)
        {

            var concurrentReservoir = new ConcurrentReservoir<int>(sizeLimit);
            Assert.AreEqual(concurrentReservoir.Size, sizeLimit);
        }

        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 1 })]
        [TestCase(new[] { 1, 1, 2 })]
        public void concurrentReservoir_FunctionsAsNormalList_ForSingleThreadedAccess(params int[] numbersToAdd)
        {
            // Because nothing interesting happens when the reservoir's item count is below the size limit, it seems reasonable to just wrap all of the basic list API tests into one test

            // Add
            foreach (var number in numbersToAdd)
                _concurrentReservoir.Add(number);

            // GetEnumerator
            var index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentReservoir).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.AreEqual(numbersToAdd[index++], nongenericEnumerator.Current);
            Assert.AreEqual(numbersToAdd.Length, index);

            // Count
            Assert.AreEqual(_concurrentReservoir.Count, numbersToAdd.Length);

            // CopyTo
            var destinationArray = new int[numbersToAdd.Length];
            _concurrentReservoir.CopyTo(destinationArray, 0);
            Assert.True(numbersToAdd.SequenceEqual(destinationArray));

            // Contains
            Assert.True(numbersToAdd.All(_concurrentReservoir.Contains));

            // Clear
            _concurrentReservoir.Clear();
            Assert.AreEqual(0, _concurrentReservoir.Count);
            Assert.False(numbersToAdd.Any(_concurrentReservoir.Contains));
        }

        [Test]
        public void concurrentReservoir_ResizeChangesMaximumItemsAllowed()
        {
            // Resize
            _concurrentReservoir.Add(1);
            _concurrentReservoir.Add(2);
            _concurrentReservoir.Add(3);
            Assert.AreEqual(3, _concurrentReservoir.Count);
            _concurrentReservoir.Resize(2);
            Assert.AreEqual(2, _concurrentReservoir.Count);
            _concurrentReservoir.Add(4);
            Assert.AreEqual(2, _concurrentReservoir.Count);
        }

        [Test]
        public void concurrentReservoir_SamplesItemsWhenSizeLimitReached()
        {
            var numberOfItemsToAddAfterReservoirLimitReached = 10000;
            _concurrentReservoir.Resize(10000);
            for (var i = 0; i < 10000; i++)
            {
                _concurrentReservoir.Add(0);
            }
            for (var i = 0; i < numberOfItemsToAddAfterReservoirLimitReached; i++)
            {
                _concurrentReservoir.Add(1);
                if (_concurrentReservoir.Contains(1))
                    return;
            }
            Assert.True(false, "Reservoir did not contain any of the attempted additions.");
        }

        [Test]
        public void concurrentReservoir_IsThreadSafe()
        {
            // Note: this test does not definitively prove that the collection is thread-safe, but any thread-safety test is better than no thread safety test.
            var random = new Random();

            var tasks = Enumerable.Range(1, 100)
                .Select(_ =>
                {
                    var numbersToAdd = new[] { random.Next(), random.Next(), random.Next() };
                    Action testAction = () => ExerciseFullApi(_concurrentReservoir, numbersToAdd);
                    return new Task(testAction);
                })
                .ToList();

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
        }

        [Test]
        public void concurrentReservoir_GetAddAttemptsCount()
        {
            Assert.AreEqual((ulong)0, _concurrentReservoir.GetAddAttemptsCount());
            _concurrentReservoir.Add(1);
            _concurrentReservoir.Add(2);
            _concurrentReservoir.Add(3);
            Assert.AreEqual((ulong)3, _concurrentReservoir.GetAddAttemptsCount());
        }

        private static void ExerciseFullApi(IResizableCappedCollection<int> concurrentReservoir, int[] numbersToAdd)
        {
            dynamic _;

            // Add
            foreach (var number in numbersToAdd)
                concurrentReservoir.Add(number);

            var index = 0;
            var genericEnumerator = concurrentReservoir.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
            {
                _ = genericEnumerator.Current;
            }

            index = 0;
            var nongenericEnumerator = ((IEnumerable)concurrentReservoir).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
            {
                _ = nongenericEnumerator.Current;
            }

            _ = concurrentReservoir.Count;

            var destinationArray = new int[500];
            concurrentReservoir.CopyTo(destinationArray, 0);
            _ = concurrentReservoir.Contains(numbersToAdd.First());

            try
            {
                concurrentReservoir.Remove(numbersToAdd.First());
            }
            catch (NotSupportedException)
            {
            }

            concurrentReservoir.Clear();
        }
    }
}
