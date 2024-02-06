// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Providers.Storage.CallContext;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using NewRelic.Agent.Core.CallStack;

namespace NewRelic.Providers.CallStack.AsyncLocalTests
{

    [TestFixture]
    public class CallStackTrackerTests
    {
        private ICallStackManager _tracker;

        [SetUp]
        public void SetUp()
        {
            _tracker = new SyncToAsyncCallStackManager(new CallContextStorage<int?>("mykey"));
            _tracker.AttachToAsync();
        }

        [TearDown]
        public void TestDown()
        {
            _tracker.Clear();
        }

        [Test]
        public void Pop_RemovesTopElement()
        {
            _tracker.Push(666);
            Assert.That(_tracker.TryPeek().HasValue, Is.True);
            _tracker.TryPop(666, null);

            Assert.That(_tracker.TryPeek().HasValue, Is.False);
        }

        [Test]
        public void SwitchToAsync()
        {
            var tracker = new SyncToAsyncCallStackManager(new CallContextStorage<int?>("mykey"));
            tracker.Push(666);
            Assert.That(tracker.TryPeek().HasValue, Is.True);

            tracker.AttachToAsync();

            Assert.That(tracker.TryPeek(), Is.EqualTo(666));
        }

        [Test]
        public void Peek_ReturnsEmpty_IfStackIsEmpty()
        {
            Assert.That(_tracker.TryPeek(), Is.Null);
        }

        [Test]
        public void Peek_ReturnsPushedItem_IfOneItemIsPushed()
        {
            var id = 666;
            _tracker.Push(id);

            Assert.That(_tracker.TryPeek(), Is.EqualTo(id));
        }

        [Test]
        public void Peek_SuccessivelyReturnMostRecentPushedItem_IfMultipleItemsArePushedAndThenPopped()
        {
            var id1 = 1;
            var id2 = 2;
            _tracker.Push(id1);
            _tracker.Push(id2);

            Assert.That(_tracker.TryPeek(), Is.EqualTo(id2));
            _tracker.TryPop(id2, id1);
            Assert.That(_tracker.TryPeek(), Is.EqualTo(id1));
        }

        [Test]
        public void Peek_DoesNotModifyStack()
        {
            var id = 1;
            _tracker.Push(id);

            Assert.That(_tracker.TryPeek(), Is.EqualTo(id));
            Assert.That(_tracker.TryPeek(), Is.EqualTo(id));
        }

        [Test]
        public void ClearFrames_RemovesAllFrames()
        {
            _tracker.Push(1);
            _tracker.Push(2);
            _tracker.Push(3);
            _tracker.Clear();

            Assert.That(_tracker.TryPeek().HasValue, Is.False);
        }

        [Test]
        public void AsyncLocalCallStackTracker_MaintainsDifferentStacks_PerThread()
        {
            _tracker.Push(1);
            var thread2Task = Task.Run(() =>
            {
                _tracker.Push(2);
                return _tracker.TryPeek();
            });
            thread2Task.Wait();

            var thread1Top = _tracker.TryPeek();
            var thread2Top = thread2Task.Result;

            NrAssert.Multiple(
                () => Assert.That(thread1Top, Is.Not.Null),
                () => Assert.That(thread2Top, Is.Not.Null),
                () => Assert.That(thread2Top, Is.Not.EqualTo(thread1Top))
                );
        }

    }

}
