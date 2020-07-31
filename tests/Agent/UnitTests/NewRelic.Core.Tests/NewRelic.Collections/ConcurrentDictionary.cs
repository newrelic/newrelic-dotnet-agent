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
    [TestFixture]
    public class Class_ConcurrentDictionary
    {
        private ConcurrentDictionary<int, string> _concurrentDictionary;

        public Class_ConcurrentDictionary()
        {
            _concurrentDictionary = new ConcurrentDictionary<int, string>();
        }

        [SetUp]
        public void Setup()
        {
            _concurrentDictionary = new ConcurrentDictionary<int, string>();
        }

        [TestCase(1, "1", 1, "1")]
        [TestCase(1, null, 1, "1")]
        [TestCase(1, "1", 2, "2")]
        public void GetOrSetValue_ReturnsCorrectValues(int key, string value, int searchKey, string expectedValue)
        {
            // Arrange 
            _concurrentDictionary.Add(key, value);

            // Act
            var returnedValue = _concurrentDictionary.GetOrSetValue(searchKey, searchKey.ToString);

            // Assert
            var persistedValue = _concurrentDictionary[searchKey];
            Assert.AreEqual(expectedValue, returnedValue);
            Assert.AreEqual(expectedValue, persistedValue);
        }

        [Test]
        public void GetOrSetValue_Throws_IfGetNewValueReturnsNull()
        {
            Assert.Throws<NullReferenceException>(() => _concurrentDictionary.GetOrSetValue(1, () => null));
        }

        [Test]
        public void Merge_InsertsNewValue_IfNoExistingValue()
        {
            const int key = 1;
            _concurrentDictionary.Merge(key, "foo", (existing, next) => { throw new Exception("Merge function shouldn't have been called"); });

            Assert.AreEqual("foo", _concurrentDictionary[key]);
        }

        [TestCase(null, null, "")]
        [TestCase("foo", null, "foo")]
        [TestCase(null, "bar", "bar")]
        [TestCase("foo", "bar", "foobar")]
        public void Merge_CallsMergeFunctionAsNeeded(string existingValue, string newValue, string expectedMergedValue)
        {
            const int key = 1;
            _concurrentDictionary.Add(key, existingValue);

            _concurrentDictionary.Merge(key, newValue, (existing, next) => existing + next);

            Assert.AreEqual(expectedMergedValue, _concurrentDictionary[key]);
        }

        [TestCase(new[] { 1 })]
        [TestCase(new[] { 1, 1 })]
        [TestCase(new[] { 1, 1, 2 })]
        public void ConcurrentDictionary_FunctionsAsNormalDictionary_ForSingleThreadedAccess(int[] numbersToAdd)
        {
            // Because we're not doing anything interesting with the dictionary itself, it seems reasonable to just wrap all of the basic dictionary API tests into one test

            var distinctNumberPairs = numbersToAdd.Distinct().Select(number => new KeyValuePair<int, string>(number, number.ToString())).ToList();

            // Indexer -- Set
            foreach (var number in numbersToAdd)
                _concurrentDictionary[number] = number.ToString();

            // Indexer -- Get
            foreach (var number in numbersToAdd)
                Assert.AreEqual(_concurrentDictionary[number], number.ToString());

            // GetEnumerator<T>
            var index = 0;
            var genericEnumerator = _concurrentDictionary.GetEnumerator();
            while (index < numbersToAdd.Length && genericEnumerator.MoveNext())
                Assert.AreEqual(distinctNumberPairs[index++], genericEnumerator.Current);
            Assert.AreEqual(distinctNumberPairs.Count, index);

            // GetEnumerator
            index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentDictionary).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.AreEqual(distinctNumberPairs[index++], nongenericEnumerator.Current);
            Assert.AreEqual(distinctNumberPairs.Count, index);

            // Count
            Assert.AreEqual(_concurrentDictionary.Count, distinctNumberPairs.Count);

            // CopyTo
            var destinationArray = new KeyValuePair<int, string>[distinctNumberPairs.Count];
            _concurrentDictionary.CopyTo(destinationArray, 0);
            Assert.True(distinctNumberPairs.SequenceEqual(destinationArray));

            // Contains
            Assert.True(distinctNumberPairs.All(_concurrentDictionary.Contains));

            // ContainsKey
            Assert.True(numbersToAdd.All(_concurrentDictionary.ContainsKey));

            // Keys
            Assert.True(distinctNumberPairs.Select(kvp => kvp.Key).SequenceEqual(_concurrentDictionary.Keys));

            // Values
            Assert.True(distinctNumberPairs.Select(kvp => kvp.Value).SequenceEqual(_concurrentDictionary.Values));

            // TryGetValue
            var largeNumber = numbersToAdd.Max() + 1;
            string outValue;
            _concurrentDictionary.TryGetValue(numbersToAdd.First(), out outValue);
            Assert.AreEqual(numbersToAdd.First().ToString(), outValue);
            _concurrentDictionary.TryGetValue(largeNumber, out outValue);
            Assert.Null(outValue);

            // Add
            _concurrentDictionary.Add(largeNumber, largeNumber.ToString());
            Assert.AreEqual(_concurrentDictionary[largeNumber], largeNumber.ToString());
            Assert.Throws<ArgumentException>(() => _concurrentDictionary.Add(largeNumber, largeNumber.ToString()));

            // Remove
            _concurrentDictionary.Remove(distinctNumberPairs.First());
            Assert.False(_concurrentDictionary.Contains(distinctNumberPairs.First()));

            // Clear
            _concurrentDictionary.Clear();
            Assert.AreEqual(0, _concurrentDictionary.Count);
            Assert.False(distinctNumberPairs.Any(_concurrentDictionary.Contains));
        }

        [Test]
        public void ConcurrentDictionary_IsThreadSafe()
        {
            // Note: this test does not definitively prove that the collection is thread-safe,
            // but any thread-safety test is better than no thread safety test.
            var random = new Random();

            var tasks = Enumerable.Range(1, 100)
                .Select(_ =>
                {
                    var numbersToAdd = new[] { random.Next(), random.Next(), random.Next() };
                    Action testAction = () => ExerciseFullApi(_concurrentDictionary, numbersToAdd);
                    return new Task(testAction);
                })
                .ToList();

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
        }

        private static void ExerciseFullApi(IDictionary<int, string> concurrentDictionary, ICollection<int> numbersToAdd)
        {
            dynamic _;

            var distinctNumberPairs = numbersToAdd.Distinct().Select(number => new KeyValuePair<int, string>(number, number.ToString())).ToList();

            foreach (var number in numbersToAdd)
                concurrentDictionary[number] = number.ToString();

            try
            {
                // This operation can throw if another thread clears the collection
                _ = concurrentDictionary[numbersToAdd.First()];
            }
            catch (KeyNotFoundException)
            {
            }

            var index = 0;
            var genericEnumerator = concurrentDictionary.GetEnumerator();
            while (index < numbersToAdd.Count && genericEnumerator.MoveNext())
            {
                _ = genericEnumerator.Current;
            }

            index = 0;
            var nongenericEnumerator = ((IEnumerable)concurrentDictionary).GetEnumerator();
            while (index < numbersToAdd.Count && nongenericEnumerator.MoveNext())
            {
                _ = nongenericEnumerator.Current;
            }

            _ = concurrentDictionary.Count;

            var destinationArray = new KeyValuePair<int, string>[500];
            concurrentDictionary.CopyTo(destinationArray, 0);
            _ = concurrentDictionary.Contains(distinctNumberPairs.First());
            _ = concurrentDictionary.ContainsKey(numbersToAdd.First());
            _ = concurrentDictionary.Keys;
            _ = concurrentDictionary.Values;

            var largeNumber = numbersToAdd.Max() + 1;
            string outValue;
            concurrentDictionary.TryGetValue(numbersToAdd.First(), out outValue);
            concurrentDictionary.TryGetValue(largeNumber, out outValue);

            try
            {
                // This operation can throw if another thread adds a similar value
                concurrentDictionary.Add(largeNumber, largeNumber.ToString());
            }
            catch (ArgumentException)
            {
            }

            concurrentDictionary.Remove(distinctNumberPairs.First());
            concurrentDictionary.Clear();
        }
    }
}
