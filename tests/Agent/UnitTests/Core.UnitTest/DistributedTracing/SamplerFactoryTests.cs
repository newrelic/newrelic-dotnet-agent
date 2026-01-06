// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class SamplerFactoryTests
    {
        private SamplerFactory _samplerFactory;

        [SetUp]
        public void SetUp()
        {
            _samplerFactory = new SamplerFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _samplerFactory?.Dispose();
        }

        [Test]
        public void AdaptiveSampler_ReturnsSameInstance()
        {
            var sampler1 = _samplerFactory.GetSampler(SamplerType.Adaptive, null);
            var sampler2 = _samplerFactory.GetSampler(SamplerType.Adaptive, null);

            Assert.That(sampler1, Is.SameAs(sampler2), "Adaptive sampler should be a singleton instance within the factory.");
        }

        [Test]
        public void AlwaysOnSampler_ReturnsSingleton()
        {
            var sampler1 = _samplerFactory.GetSampler(SamplerType.AlwaysOn, null);
            var sampler2 = _samplerFactory.GetSampler(SamplerType.AlwaysOn, null);

            Assert.That(sampler1, Is.SameAs(sampler2), "AlwaysOn sampler should return the same singleton instance.");
            Assert.That(sampler1, Is.TypeOf<AlwaysOnSampler>());
        }

        [Test]
        public void AlwaysOffSampler_ReturnsSingleton()
        {
            var sampler1 = _samplerFactory.GetSampler(SamplerType.AlwaysOff, null);
            var sampler2 = _samplerFactory.GetSampler(SamplerType.AlwaysOff, null);

            Assert.That(sampler1, Is.SameAs(sampler2), "AlwaysOff sampler should return the same singleton instance.");
            Assert.That(sampler1, Is.TypeOf<AlwaysOffSampler>());
        }

        [Test]
        public void TraceIdRatioBased_WithRatio_ReturnsNewInstances()
        {
            var sampler1 = _samplerFactory.GetSampler(SamplerType.TraceIdRatioBased, 0.25f);
            var sampler2 = _samplerFactory.GetSampler(SamplerType.TraceIdRatioBased, 0.25f);

            Assert.That(sampler1, Is.Not.SameAs(sampler2), "TraceIdRatioSampler should be stateless and newly instantiated each call.");
            Assert.That(sampler1, Is.TypeOf<TraceIdRatioBasedSampler>());
            Assert.That(sampler2, Is.TypeOf<TraceIdRatioBasedSampler>());
        }

        [Test]
        public void TraceIdRatioBased_WithoutRatio_FallsBackToAdaptiveAndLogsWarning()
        {
            var adaptive = _samplerFactory.GetSampler(SamplerType.Adaptive, null);

            var fallback = _samplerFactory.GetSampler(SamplerType.TraceIdRatioBased, null);

            Assert.That(fallback, Is.SameAs(adaptive), "Missing ratio should fall back to adaptive sampler.");
        }
    }
}
