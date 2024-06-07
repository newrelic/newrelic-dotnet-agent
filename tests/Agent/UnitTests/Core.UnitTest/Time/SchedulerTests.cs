// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;
using NewRelic.Agent.Core.Fixtures;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Time
{
    [TestFixture]
    public class SchedulerTests
    {
        private Scheduler _scheduler;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new Scheduler();
        }

        [TearDown]
        public void TearDown()
        {
            _scheduler.Dispose();
        }

        [Test]
        public void ExecuteOnce_ExecutesTheGivenAction()
        {
            var wasExecuted = false;

            _scheduler.ExecuteOnce(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

            AssertEventuallyTrue(() => wasExecuted);
        }

        [Test]
        public void ExecuteOnce_LogsExceptions()
        {
            using (var logging = new TestUtilities.Logging())
            {
                _scheduler.ExecuteOnce(() =>
                {
                    throw new Exception();
                }, TimeSpan.FromMilliseconds(1));
                AssertEventuallyTrue(() => logging.ErrorCount == 1);
            }
        }

        [Test]
        public void ExecuteEvery_ExecutesTheGivenAction()
        {
            var wasExecuted = false;

            _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));

            AssertEventuallyTrue(() => wasExecuted);
        }

        [Test]
        public void ExecuteEvery_ExecutesTheGivenActionMultipleTimes()
        {
            var wasExecuted = false;

            _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));
            AssertEventuallyTrue(() => wasExecuted);
            wasExecuted = false;

            AssertEventuallyTrue(() => wasExecuted);
        }

        [Test]
        public void ExecuteEvery_LogsExceptions()
        {
            using (var logging = new TestUtilities.Logging())
            {
                _scheduler.ExecuteEvery(() =>
                {
                    throw new Exception();
                }, TimeSpan.FromMilliseconds(1));

                AssertEventuallyTrue(() => logging.ErrorCount >= 1);

            }
        }

        [Test]
        public void StopExecuting_StopsTheGivenActionFromExecutingAgain()
        {
            var wasExecuted = false;
            Action setWasExecuted = () => wasExecuted = true;

            _scheduler.ExecuteEvery(setWasExecuted, TimeSpan.FromMilliseconds(1));
            AssertEventuallyTrue(() => wasExecuted);

            _scheduler.StopExecuting(setWasExecuted);
            Thread.Sleep(TimeSpan.FromMilliseconds(5));
            wasExecuted = false;
            Thread.Sleep(TimeSpan.FromMilliseconds(5));

            Assert.That(wasExecuted, Is.False);
        }

        [Test]
        public void StopExecuting_DoesNotStopOtherActions()
        {
            var wasExecuted = false;

            _scheduler.ExecuteEvery(() => wasExecuted = true, TimeSpan.FromMilliseconds(1));
            _scheduler.StopExecuting(() => { });
            wasExecuted = false;

            AssertEventuallyTrue(() => wasExecuted);
        }

        private static void AssertEventuallyTrue(Func<bool> wasExecutedFunc)
        {
            Assertions.Eventually(wasExecutedFunc, TimeSpan.FromSeconds(5));
        }
    }
}
