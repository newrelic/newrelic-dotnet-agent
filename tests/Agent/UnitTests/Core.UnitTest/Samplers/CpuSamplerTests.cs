// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Transformers;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Samplers
{
    [TestFixture]
    public class CpuSamplerTests
    {
        private CpuSampler _cpuSampler;

        private ICpuSampleTransformer _cpuSampleTransformer;

        private Action _sampleAction;

        [SetUp]
        public void SetUp()
        {
            var scheduler = Mock.Create<IScheduler>();
            Mock.Arrange(() => scheduler.ExecuteEvery(Arg.IsAny<Action>(), Arg.IsAny<TimeSpan>(), Arg.IsAny<TimeSpan?>()))
                .DoInstead<Action, TimeSpan, TimeSpan?>((action, _, __) => _sampleAction = action);
            _cpuSampleTransformer = Mock.Create<ICpuSampleTransformer>();
            _cpuSampler = new CpuSampler(scheduler, _cpuSampleTransformer, new ProcessStatic());
            _cpuSampler.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _cpuSampler.Dispose();
        }

        [Test]
        public void cpu_sample_generated_on_sample()
        {
            // Arrange
            var cpuSample = null as ImmutableCpuSample;
            Mock.Arrange(() => _cpuSampleTransformer.Transform(Arg.IsAny<ImmutableCpuSample>()))
                .DoInstead<ImmutableCpuSample>(sample => cpuSample = sample);

            // Act
            _sampleAction();

            // Assert
            Assert.That(cpuSample, Is.Not.Null);
        }

        [Test]
        public void cpu_sample_values_increase_over_time()
        {
            // Arrange
            var cpuSampleBefore = null as ImmutableCpuSample;
            Mock.Arrange(() => _cpuSampleTransformer.Transform(Arg.IsAny<ImmutableCpuSample>()))
                .DoInstead<ImmutableCpuSample>(sample => cpuSampleBefore = sample);

            // Act
            _sampleAction();

            IncreaseUserProcessorTime(50);

            // Arrange
            var cpuSampleAfter = null as ImmutableCpuSample;
            Mock.Arrange(() => _cpuSampleTransformer.Transform(Arg.IsAny<ImmutableCpuSample>()))
                .DoInstead<ImmutableCpuSample>(sample => cpuSampleAfter = sample);

            // Act
            _sampleAction();

            // Assert
            Assert.That(cpuSampleBefore.CurrentUserProcessorTime < cpuSampleAfter.CurrentUserProcessorTime, Is.True, "UserProcessorTime did not increase as expected");
        }

        private void IncreaseUserProcessorTime(int iterations)
        {
            for (int x = iterations; x > 0; x--)
            {
                GC.Collect();
            }
        }
    }
}
