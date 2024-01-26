// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using CompositeTests;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class ThreadStatsSamplerTests
    {
        private ThreadStatsSampler _threadStatsSampler;

        private ISampledEventListener<ThreadpoolThroughputEventsSample> _threadEventsListener;

        private IThreadStatsSampleTransformer _threadStatsSampleTransformer;

        private Action _sampleAction;

        private CompositeTestAgent _compositeTestAgent;

        private IScheduler _mockScheduler = Mock.Create<IScheduler>();

        [SetUp]
        public void SetUp()
        {
            _sampleAction = null;
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.SetEventListenerSamplersEnabled(true);

            Mock.Arrange(() => _mockScheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
            _threadStatsSampleTransformer = Mock.Create<IThreadStatsSampleTransformer>();
            _threadEventsListener = Mock.Create<ISampledEventListener<ThreadpoolThroughputEventsSample>>();
            _threadStatsSampler = new ThreadStatsSampler(_mockScheduler, GetThreadEventsListener, _threadStatsSampleTransformer, new ThreadPoolStatic());
            _threadStatsSampler.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _threadStatsSampler.Dispose();
            _compositeTestAgent.Dispose();
            _threadEventsListener.Dispose();
        }

        [Test]
        public void ConstructingAThreadStatsSamplerShouldNotThrowAnError()
        {
            //This test was added because the AbstractSampler used to make a call to the virtual method Start in its .ctor,
            //which then invoked the overridden Start method in the child class before the child .ctor had a chance to
            //initialize its fields. This test helps with verifying that this problem doesn't happen again.
            var scheduler = Mock.Create<IScheduler>();
            var sampler = new ThreadStatsSampler(scheduler, GetThreadEventsListener, _threadStatsSampleTransformer, new ThreadPoolStatic());
            sampler.Start();
        }

        [Test]
        public void threadpool_usage_sample_generated_on_sample()
        {
            // Arrange
            var threadpoolUsageStatsSample = null as ThreadpoolUsageStatsSample;
            Mock.Arrange(() => _threadStatsSampleTransformer.Transform(Arg.IsAny<ThreadpoolUsageStatsSample>()))
                .DoInstead<ThreadpoolUsageStatsSample>(sample => threadpoolUsageStatsSample = sample);

            // Act
            _sampleAction();

            // Assert
            Assert.That(threadpoolUsageStatsSample, Is.Not.Null);
        }

        [Test]
        public void threadpool_throughput_sample_generated_on_sample()
        {
            // Arrange
            var threadpoolThroughputStatsSample = null as ThreadpoolThroughputEventsSample;
            Mock.Arrange(() => _threadStatsSampleTransformer.Transform(Arg.IsAny<ThreadpoolThroughputEventsSample>()))
                .DoInstead<ThreadpoolThroughputEventsSample>(sample => threadpoolThroughputStatsSample = sample);

            // Act
            _sampleAction();

            // Assert
            Assert.That(threadpoolThroughputStatsSample, Is.Not.Null);
        }

        private ISampledEventListener<ThreadpoolThroughputEventsSample> GetThreadEventsListener()
        {
            return _threadEventsListener;
        }
    }
}
