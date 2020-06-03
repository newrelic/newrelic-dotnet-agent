using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;




namespace NewRelic.Collections.UnitTests
{
    // ReSharper disable once InconsistentNaming
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
                Assert.AreEqual(numbersToAdd[index++], genericEnumerator.Current);
            Assert.AreEqual(numbersToAdd.Length, index);

            // GetEnumerator
            index = 0;
            var nongenericEnumerator = ((IEnumerable)_concurrentList).GetEnumerator();
            while (index < numbersToAdd.Length && nongenericEnumerator.MoveNext())
                Assert.AreEqual(numbersToAdd[index++], nongenericEnumerator.Current);
            Assert.AreEqual(numbersToAdd.Length, index);

            // Count
            Assert.AreEqual(_concurrentList.Count, numbersToAdd.Length);

            // CopyTo
            var destinationArray = new int[numbersToAdd.Length];
            _concurrentList.CopyTo(destinationArray, 0);
            Assert.True(numbersToAdd.SequenceEqual(destinationArray));

            // Contains
            Assert.True(numbersToAdd.All(_concurrentList.Contains));

            // Remove
            _concurrentList.Remove(numbersToAdd.First());
            Assert.True(_concurrentList.SequenceEqual(numbersToAdd.Skip(1)));

            // Insert
            _concurrentList.Insert(0, numbersToAdd.First());
            Assert.True(_concurrentList.SequenceEqual(numbersToAdd));

            // IndexOf
            index = _concurrentList.IndexOf(numbersToAdd.First());
            Assert.AreEqual(0, index);

            // RemoveAt
            _concurrentList.RemoveAt(0);
            Assert.True(_concurrentList.SequenceEqual(numbersToAdd.Skip(1)));

            // Indexer -- Set
            _concurrentList.Insert(0, numbersToAdd.First());
            _concurrentList[0] = _concurrentList[0] + 1;
            Assert.AreEqual(numbersToAdd.First() + 1, _concurrentList.First());

            // Indexer -- Get
            Assert.AreEqual(numbersToAdd.First() + 1, _concurrentList[0]);

            // Clear
            _concurrentList.Clear();
            Assert.AreEqual(0, _concurrentList.Count);
            Assert.False(numbersToAdd.Any(_concurrentList.Contains));
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

            // ReSharper disable PossibleNullReferenceException
            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());
            // ReSharper restore PossibleNullReferenceException
        }

        // ReSharper disable RedundantAssignment
        private static void ExerciseFullApi(IList<int> concurrentList, int[] numbersToAdd)
        {
            // ReSharper disable once NotAccessedVariable
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
        // ReSharper restore RedundantAssignment
    }
}
