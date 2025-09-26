// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DistributedTracing.Samplers;
using NewRelic.Agent.Core.Events;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class SamplerServiceTests
    {
        private ISamplerFactory _mockFactory;
        private TestSamplerService _service;
        private IConfiguration _config;
        private int _samplerId;

        [SetUp]
        public void SetUp()
        {
            _mockFactory = Mock.Create<ISamplerFactory>();
            _service = new TestSamplerService(_mockFactory);
            _config = Mock.Create<IConfiguration>();

            // Default configuration used by most tests unless overridden per-test.
            ArrangeConfig(
                rootType: SamplerType.Adaptive, rootRatio: null,
                remoteSampledType: SamplerType.AlwaysOn, remoteSampledRatio: null,
                remoteNotSampledType: SamplerType.AlwaysOff, remoteNotSampledRatio: null);

            // Provide a unique sampler instance per factory invocation so we can detect rebuilds.
            Mock.Arrange(() => _mockFactory.GetSampler(Arg.IsAny<SamplerType>(), Arg.IsAny<float?>()))
                .DoInstead<SamplerType, float?>((t, r) => { })
                .Returns<SamplerType, float?>((t, r) => new DummySampler(++_samplerId))
                .OccursAtLeast(0);

            _service.OverrideConfigForTesting(_config);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
        }

        [Test]
        public void GetSampler_InitializesAndReturnsExpectedInstances()
        {
            var root = _service.GetSampler(SamplerLevel.Root);
            var remoteSampled = _service.GetSampler(SamplerLevel.RemoteParentSampled);
            var remoteNotSampled = _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            Assert.Multiple(() =>
            {
                Assert.That(root, Is.Not.Null);
                Assert.That(remoteSampled, Is.Not.Null);
                Assert.That(remoteNotSampled, Is.Not.Null);
                // Factory should have been called exactly 3 times (once per level)
                Mock.Assert(_mockFactory);
            });
        }

        [Test]
        public void GetSampler_CachesInstances_SubsequentCallsDoNotInvokeFactoryAgain()
        {
            // First access (creates)
            var firstRoot = _service.GetSampler(SamplerLevel.Root);
            var firstRemoteSampled = _service.GetSampler(SamplerLevel.RemoteParentSampled);
            var firstRemoteNotSampled = _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            // Record current factory call count by id max
            var maxIdAfterFirstInit = new[] { firstRoot, firstRemoteSampled, firstRemoteNotSampled }
                .Cast<DummySampler>()
                .Max(s => s.Id);

            // Second access (should be cached)
            var secondRoot = _service.GetSampler(SamplerLevel.Root);
            var secondRemoteSampled = _service.GetSampler(SamplerLevel.RemoteParentSampled);
            var secondRemoteNotSampled = _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            Assert.Multiple(() =>
            {
                Assert.That(secondRoot, Is.SameAs(firstRoot));
                Assert.That(secondRemoteSampled, Is.SameAs(firstRemoteSampled));
                Assert.That(secondRemoteNotSampled, Is.SameAs(firstRemoteNotSampled));
                Assert.That(_samplerId, Is.EqualTo(maxIdAfterFirstInit), "Factory should not be re-invoked after caching.");
            });
        }

        [Test]
        public void ConfigurationUpdate_ClearsCache_RebuildsWithNewInstances()
        {
            // Initial retrieval
            var origRoot = _service.GetSampler(SamplerLevel.Root);
            var origSampled = _service.GetSampler(SamplerLevel.RemoteParentSampled);
            var origNotSampled = _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            var originalIds = new[] { origRoot, origSampled, origNotSampled }.Cast<DummySampler>().Select(s => s.Id).ToArray();

            // Change configuration to new sampler types (force different creation path)
            ArrangeConfig(
                rootType: SamplerType.AlwaysOn, rootRatio: null,
                remoteSampledType: SamplerType.TraceIdRatioBased, remoteSampledRatio: 0.5f,
                remoteNotSampledType: SamplerType.TraceIdRatioBased, remoteNotSampledRatio: 0.1f);

            _service.OverrideConfigForTesting(_config);

            // Trigger configuration updated invalidation
            _service.TriggerConfigUpdated();

            var newRoot = _service.GetSampler(SamplerLevel.Root);
            var newSampled = _service.GetSampler(SamplerLevel.RemoteParentSampled);
            var newNotSampled = _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            var newIds = new[] { newRoot, newSampled, newNotSampled }.Cast<DummySampler>().Select(s => s.Id).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(newRoot, Is.Not.SameAs(origRoot));
                Assert.That(newSampled, Is.Not.SameAs(origSampled));
                Assert.That(newNotSampled, Is.Not.SameAs(origNotSampled));
                Assert.That(newIds.Intersect(originalIds), Is.Empty, "All sampler instances should be rebuilt after config update.");
            });
        }

        [Test]
        public void ConcurrentAccess_InitializesOnlyOncePerSampler()
        {
            // Configure all sampler levels to use a ratio-based sampler to exercise factory calls uniformly.
            ArrangeConfig(
                rootType: SamplerType.TraceIdRatioBased, rootRatio: 0.3f,
                remoteSampledType: SamplerType.TraceIdRatioBased, remoteSampledRatio: 0.4f,
                remoteNotSampledType: SamplerType.TraceIdRatioBased, remoteNotSampledRatio: 0.5f);
            _service.OverrideConfigForTesting(_config);

            // Using separate bags removes reliance on ordering of a single ConcurrentBag (its internal enumeration order
            // is not guaranteed and caused intermittent failures when using index % 3 bucketing).
            var rootSamplers = new ConcurrentBag<ISampler>();
            var remoteSampledSamplers = new ConcurrentBag<ISampler>();
            var remoteNotSampledSamplers = new ConcurrentBag<ISampler>();

            Parallel.For(0, 100, _ =>
            {
                rootSamplers.Add(_service.GetSampler(SamplerLevel.Root));
                remoteSampledSamplers.Add(_service.GetSampler(SamplerLevel.RemoteParentSampled));
                remoteNotSampledSamplers.Add(_service.GetSampler(SamplerLevel.RemoteParentNotSampled));
            });

            var rootDistinct = rootSamplers.Distinct().Count();
            var remoteSampledDistinct = remoteSampledSamplers.Distinct().Count();
            var remoteNotSampledDistinct = remoteNotSampledSamplers.Distinct().Count();

            Assert.Multiple(() =>
            {
                Assert.That(rootDistinct, Is.EqualTo(1), "Root sampler should be initialized exactly once.");
                Assert.That(remoteSampledDistinct, Is.EqualTo(1), "RemoteParentSampled sampler should be initialized exactly once.");
                Assert.That(remoteNotSampledDistinct, Is.EqualTo(1), "RemoteParentNotSampled sampler should be initialized exactly once.");
                Assert.That(_samplerId, Is.EqualTo(3), "Factory should have been invoked exactly once per sampler level.");
            });
        }

        [Test]
        public void Ratios_ArePassedThroughToFactory()
        {
            var captured = new List<(SamplerType type, float? ratio)>();

            Mock.Arrange(() => _mockFactory.GetSampler(Arg.IsAny<SamplerType>(), Arg.IsAny<float?>()))
                .DoInstead<SamplerType, float?>((t, r) => captured.Add((t, r)))
                .Returns<SamplerType, float?>((t, r) => new DummySampler(++_samplerId));

            ArrangeConfig(
                rootType: SamplerType.TraceIdRatioBased, rootRatio: 0.11f,
                remoteSampledType: SamplerType.TraceIdRatioBased, remoteSampledRatio: 0.22f,
                remoteNotSampledType: SamplerType.TraceIdRatioBased, remoteNotSampledRatio: 0.33f);
            _service.OverrideConfigForTesting(_config);

            _service.GetSampler(SamplerLevel.Root);
            _service.GetSampler(SamplerLevel.RemoteParentSampled);
            _service.GetSampler(SamplerLevel.RemoteParentNotSampled);

            Assert.That(captured, Is.EquivalentTo(new[]
            {
                (SamplerType.TraceIdRatioBased, (float?)0.11f),
                (SamplerType.TraceIdRatioBased, (float?)0.22f),
                (SamplerType.TraceIdRatioBased, (float?)0.33f)
            }));
        }

        private void ArrangeConfig(
            SamplerType rootType, float? rootRatio,
            SamplerType remoteSampledType, float? remoteSampledRatio,
            SamplerType remoteNotSampledType, float? remoteNotSampledRatio)
        {
            Mock.Arrange(() => _config.RootSamplerType).Returns(rootType);
            Mock.Arrange(() => _config.RootTraceIdRatioSamplerRatio).Returns(rootRatio);
            Mock.Arrange(() => _config.RemoteParentSampledSamplerType).Returns(remoteSampledType);
            Mock.Arrange(() => _config.RemoteParentSampledTraceIdRatioSamplerRatio).Returns(remoteSampledRatio);
            Mock.Arrange(() => _config.RemoteParentNotSampledSamplerType).Returns(remoteNotSampledType);
            Mock.Arrange(() => _config.RemoteParentNotSampledTraceIdRatioSamplerRatio).Returns(remoteNotSampledRatio);
        }

        private sealed class DummySampler : ISampler
        {
            public int Id { get; }
            public DummySampler(int id) => Id = id;
            public ISamplingResult ShouldSample(ISamplingParameters samplingParameters) => new SamplingResult(false, samplingParameters.Priority);
            public void StartTransaction() { }
        }

        private sealed class TestSamplerService : SamplerService
        {
            public TestSamplerService(ISamplerFactory samplerFactory) : base(samplerFactory) { }
            public void TriggerConfigUpdated() => OnConfigurationUpdated(ConfigurationUpdateSource.Local);
        }
    }
}
