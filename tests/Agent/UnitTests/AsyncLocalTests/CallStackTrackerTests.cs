// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Tasks;
using NewRelic.Providers.Storage.CallContext;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using NewRelic.Agent.Core.CallStack;
using NUnit.Framework.Legacy;

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
            ClassicAssert.IsTrue(_tracker.TryPeek().HasValue);
            _tracker.TryPop(666, null);

            ClassicAssert.IsFalse(_tracker.TryPeek().HasValue);
        }

        [Test]
        public void SwitchToAsync()
        {
            var tracker = new SyncToAsyncCallStackManager(new CallContextStorage<int?>("mykey"));
            tracker.Push(666);
            ClassicAssert.IsTrue(tracker.TryPeek().HasValue);

            tracker.AttachToAsync();

            ClassicAssert.AreEqual(666, tracker.TryPeek());
        }

        [Test]
        public void Peek_ReturnsEmpty_IfStackIsEmpty()
        {
            ClassicAssert.IsNull(_tracker.TryPeek());
        }

        [Test]
        public void Peek_ReturnsPushedItem_IfOneItemIsPushed()
        {
            var id = 666;
            _tracker.Push(id);

            ClassicAssert.AreEqual(id, _tracker.TryPeek());
        }

        [Test]
        public void Peek_SuccessivelyReturnMostRecentPushedItem_IfMultipleItemsArePushedAndThenPopped()
        {
            var id1 = 1;
            var id2 = 2;
            _tracker.Push(id1);
            _tracker.Push(id2);

            ClassicAssert.AreEqual(id2, _tracker.TryPeek());
            _tracker.TryPop(id2, id1);
            ClassicAssert.AreEqual(id1, _tracker.TryPeek());
        }

        [Test]
        public void Peek_DoesNotModifyStack()
        {
            var id = 1;
            _tracker.Push(id);

            ClassicAssert.AreEqual(id, _tracker.TryPeek());
            ClassicAssert.AreEqual(id, _tracker.TryPeek());
        }

        [Test]
        public void ClearFrames_RemovesAllFrames()
        {
            _tracker.Push(1);
            _tracker.Push(2);
            _tracker.Push(3);
            _tracker.Clear();

            ClassicAssert.IsFalse(_tracker.TryPeek().HasValue);
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
                () => ClassicAssert.NotNull(thread1Top),
                () => ClassicAssert.NotNull(thread2Top),
                () => ClassicAssert.AreNotEqual(thread1Top, thread2Top)
                );
        }

    }

}
