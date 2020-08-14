// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NewRelic.Collections.UnitTests
{
   

    [TestFixture]
    public class ConcurrentPriorityQueueTests
    {
        public class TestModel : IHasPriority
        {
            public float Priority { get; set; }
        }

        protected static int[] ConstructPriorityQueueSizes = { 0, 1, 10000, 20000 };

        protected ConcurrentPriorityQueue<PrioritizedNode<TestModel>> ConcurrentPriorityQueue;

        protected int CreateCount;

        protected PrioritizedNode<TestModel> Create(float priority)
        {
            var model = new TestModel() { Priority = priority };
            return new PrioritizedNode<TestModel>(model);
        }

        [SetUp]
        public void Setup()
        {
            ConcurrentPriorityQueue = new ConcurrentPriorityQueue<PrioritizedNode<TestModel>>(20);
        }

        [Test]
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

        [TestCaseSource("ConstructPriorityQueueSizes")]
        public void ConstructPriorityQueueOfDifferentSizes(int sizeLimit)
        {
            var concurrentPriorityQueue = new ConcurrentPriorityQueue<PrioritizedNode<TestModel>>(sizeLimit);
            Assert.AreEqual(concurrentPriorityQueue.Size, sizeLimit);
        }

        [Test]
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

        [Test]
        public void GetAddAttemptsCount()
        {
            Assert.AreEqual((ulong)0, ConcurrentPriorityQueue.GetAddAttemptsCount());
            ConcurrentPriorityQueue.Add(Create(0.1f));
            ConcurrentPriorityQueue.Add(Create(0.2f));
            ConcurrentPriorityQueue.Add(Create(0.3f));
            Assert.AreEqual((ulong)3, ConcurrentPriorityQueue.GetAddAttemptsCount());
        }

        [Test]
        public void SamplesItemsWhenSizeLimitReached()
        {
            const int numberOfItemsToAddInitially = 100;
            const int numberOfItemsToAddAfterReservoirLimitReached = 100;

            //Concurrent Priority Queue will only hold NumberOfItemsToAddInitially items.
            ConcurrentPriorityQueue.Resize(numberOfItemsToAddInitially);

            var itemsToAddInitially = new PrioritizedNode<TestModel>[numberOfItemsToAddInitially];
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

        [Test]
        public void SamplesItemsWhenSizeLimitReached_AddIEnumerable()
        {
            const int reservoirSize = 100;
            const int lowerPriority = 100;
            const int higherPriority = 300;
            const float priorityShift = 0.001f;

            //Concurrent Priority Queue will only hold NumberOfItemsToAddInitially items.
            ConcurrentPriorityQueue.Resize(reservoirSize);

            var higherPriorityItemsToAdd = new PrioritizedNode<TestModel>[reservoirSize];
            var itemsToAddInitially = new PrioritizedNode<TestModel>[reservoirSize];
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

        [Test]
        public void IsThreadSafe()
        {
            // Note: this test does not definitively prove that the collection is thread-safe, but any thread-safety test is better than no thread safety test.
            var random = new Random();

            float GenPriority() { return (float)random.NextDouble(); }

            const int countOfThreads = 100;
            ConcurrentPriorityQueue.Resize(countOfThreads * 3);

            Assert.That(ConcurrentPriorityQueue.Size, Is.EqualTo(countOfThreads * 3));

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

            tasks.ForEach(task => task.Start());
            tasks.ForEach(task => task.Wait());

            Assert.That(ConcurrentPriorityQueue, Has.Exactly(countOfThreads * 3).Items);
        }

        private static void ExerciseFullApi(IResizableCappedCollection<PrioritizedNode<TestModel>> concurrentPriorityQueue, PrioritizedNode<TestModel>[] eventsToAdd, int countOfThreads)
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
            var destinationArray = new PrioritizedNode<TestModel>[eventsToAdd.Count() * countOfThreads];
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
            for (var index = 1; index < nonnullCount; ++index)
            {
                Assert.That(destinationArray[index - 1].Data.Priority, Is.GreaterThanOrEqualTo(destinationArray[index].Data.Priority));
            }

            //make sure that remove is not supported
            Assert.That(() => concurrentPriorityQueue.Remove(eventsToAdd[0]), Throws.TypeOf<NotSupportedException>());
        }
    }
}
