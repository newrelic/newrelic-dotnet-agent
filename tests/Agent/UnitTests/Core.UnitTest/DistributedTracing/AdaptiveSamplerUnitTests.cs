// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NUnit.Framework;
using NewRelic.Agent.Core.DistributedTracing;
using NewRelic.Agent.Core.DistributedTracing.Samplers;

namespace NewRelic.Agent.Core.DistributedTracing
{
    [TestFixture]
    public class AdaptiveSamplerUnitTests
    {
        private const float PriorityBoost = 1.0f; // must match AdaptiveSampler
        private const float Epsilon = 1e-6f;

        [Test]
        public void Constructor_Throws_WhenTargetBelowMinimum()
        {
            Assert.That(() => new AdaptiveSampler(0, 60, null, false),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void ShouldSample_Honors_NewRelicPayload_SampledTrue()
        {
            var basePriority = 0.42f;
            var payload = DistributedTracePayload.TryBuildOutgoingPayload(
                "App", "acct", "app", "spanid", "traceid", "trust", basePriority, true, DateTime.UtcNow, "txid");

            var sampler = new AdaptiveSampler(10, 60, 123, false);

            var result = sampler.ShouldSample(new SamplingParameters(
                traceId: "traceid",
                priority: basePriority,
                traceContext: null,
                newRelicTraceContextWasAccepted: false,
                newRelicPayload: payload,
                newRelicPayloadWasAccepted: true));

            Assert.Multiple(() =>
            {
                Assert.That(result.Sampled, Is.True);
                Assert.That(result.Priority, Is.EqualTo(basePriority).Within(Epsilon), "Priority should come from incoming parameters (no local boost).");
            });
        }

        [Test]
        public void ShouldSample_Honors_NewRelicPayload_SampledFalse()
        {
            var basePriority = 0.77f;
            var payload = DistributedTracePayload.TryBuildOutgoingPayload(
                "App", "acct", "app", "spanid", "traceid", "trust", basePriority, false, DateTime.UtcNow, "txid");

            var sampler = new AdaptiveSampler(10, 60, 123, false);

            var result = sampler.ShouldSample(new SamplingParameters(
                traceId: "traceid",
                priority: basePriority,
                traceContext: null,
                newRelicTraceContextWasAccepted: false,
                newRelicPayload: payload,
                newRelicPayloadWasAccepted: true));

            Assert.Multiple(() =>
            {
                Assert.That(result.Sampled, Is.False);
                Assert.That(result.Priority, Is.EqualTo(basePriority).Within(Epsilon));
            });
        }

        [Test]
        public void LocalDecision_SampledTrue_BoostsPriority()
        {
            var basePriority = 0.25f;
            var sampler = new TestAdaptiveSampler(alwaysSample: true);

            var result = sampler.ShouldSample(new SamplingParameters("trace", basePriority));

            Assert.Multiple(() =>
            {
                Assert.That(result.Sampled, Is.True);
                Assert.That(result.Priority, Is.EqualTo(basePriority + PriorityBoost).Within(Epsilon));
            });
        }

        [Test]
        public void LocalDecision_SampledFalse_DoesNotBoostPriority()
        {
            var basePriority = 0.25f;
            var sampler = new TestAdaptiveSampler(alwaysSample: false);

            var result = sampler.ShouldSample(new SamplingParameters("trace", basePriority));

            Assert.Multiple(() =>
            {
                Assert.That(result.Sampled, Is.False);
                Assert.That(result.Priority, Is.EqualTo(basePriority).Within(Epsilon));
            });
        }

        [Test]
        public void UpdateSamplingTarget_InvalidValue_DefaultsAndFirstIntervalSampled()
        {
            var sampler = new AdaptiveSampler(5, 60, 42, false);

            sampler.UpdateSamplingTarget(0, 60); // invalid -> defaults to 10

            var firstFalseIndex = -1;
            for (int i = 0; i < 15; i++)
            {
                var r = sampler.ShouldSample(new SamplingParameters("t", 0.1f));
                if (!r.Sampled)
                {
                    firstFalseIndex = i;
                    break;
                }
            }

            Assert.That(firstFalseIndex, Is.EqualTo(10), "First 10 (default target) should be sampled in first interval.");
        }

        [Test]
        public void UpdateSamplingTarget_CustomValue_FirstIntervalSamplesExactlyTarget()
        {
            var sampler = new AdaptiveSampler(10, 60, 99, false);

            sampler.UpdateSamplingTarget(3, 60);

            bool[] results = Enumerable.Range(0, 8)
                .Select(i => sampler.ShouldSample(new SamplingParameters("trace", 0.0f)).Sampled)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(results.Take(3), Is.All.True, "First interval should auto-sample first N candidates.");
                Assert.That(results.Skip(3), Is.Not.Contains(true), "After N samples in first interval, unsampled until interval rollover.");
            });
        }

        [Test]
        public void ServerlessMode_StartTransaction_ManualIntervalCheck_DoesNotThrow()
        {
            // serverlessMode=true => manual interval checks
            var sampler = new AdaptiveSampler(5, 60, 7, true);

            sampler.StartTransaction(); // should safely perform manual interval check

            var result = sampler.ShouldSample(new SamplingParameters("trace", 0.5f));

            Assert.That(result, Is.Not.Null);
        }

        private sealed class TestAdaptiveSampler : AdaptiveSampler
        {
            private readonly bool _forced;
            public TestAdaptiveSampler(bool alwaysSample)
                : base(10, 60, 1, false) => _forced = alwaysSample;

            protected override bool ShouldSample() => _forced;
        }
    }
}
