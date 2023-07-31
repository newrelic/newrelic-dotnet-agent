// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Time
{
    public class SimpleSchedulingServiceTests
    {
        private Scheduler _scheduler;
        private SimpleSchedulingService _simpleSchedulingService;

        [SetUp]
        public void SetUp()
        {
            _scheduler = new Scheduler();
            _simpleSchedulingService = new SimpleSchedulingService(_scheduler);
        }

        [TearDown]
        public void TearDown()
        {
            _simpleSchedulingService.Dispose();
            _scheduler.Dispose();
        }

        [Test]
        public void StartAndStopTest()
        {
            _simpleSchedulingService.StartExecuteEvery(DoWork, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
            _simpleSchedulingService.StartExecuteEvery(MoreWork, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            var sssFieldType = typeof(SimpleSchedulingService).GetField("_executingActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = sssFieldType.GetValue(_simpleSchedulingService) as List<Action>;

            Assert.That(value.Count, Is.EqualTo(2));

            var action = value.FirstOrDefault(a => a.Method.Name == "DoWork");

            Assert.NotNull(action);

            _simpleSchedulingService.StopExecuting(DoWork);

            var noAction = value.FirstOrDefault(a => a.Method.Name == "DoWork");

            Assert.Null(noAction);
            Assert.That(value.Count, Is.EqualTo(1));
        }

        [Test]
        public void DisposeTest()
        {
            _simpleSchedulingService.StartExecuteEvery(DoWork, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            var sssFieldType = typeof(SimpleSchedulingService).GetField("_executingActions", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var value = sssFieldType.GetValue(_simpleSchedulingService) as List<Action>;
            var action = value.FirstOrDefault(a => a.Method.Name == "DoWork");

            Assert.NotNull(action);

            _simpleSchedulingService.Dispose();

            var noAction = value.FirstOrDefault(a => a.Method.Name == "DoWork");

            Assert.Null(noAction);
        }

        private void DoWork()
        {

        }

        private void MoreWork()
        {

        }
    }
}
