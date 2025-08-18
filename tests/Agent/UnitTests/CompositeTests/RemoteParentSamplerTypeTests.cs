// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using System.Collections.Generic;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace CompositeTests
{
    public class RemoteParentSamplerTypeTests
    {
        private CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent?.Dispose();
        }

        [TestCase(SamplerType.AlwaysOn, "01", true, null, TestName = "AlwaysOn_WithSampledTraceparent")]
        [TestCase(SamplerType.AlwaysOn, "00", true, null, TestName = "AlwaysOn_WithNotSampledTraceparent")]
        [TestCase(SamplerType.AlwaysOff, "01", false, null, TestName = "AlwaysOff_WithSampledTraceparent")]
        [TestCase(SamplerType.AlwaysOff, "00", false, null, TestName = "AlwaysOff_WithNotSampledTraceparent")]
        [TestCase(SamplerType.Default, "01", true, null, TestName = "Default_WithSampledTraceparent")]
        [TestCase(SamplerType.Default, "00", true, null, TestName = "Default_WithNotSampledTraceparent")] // uses existing logic
        // TraceIdRatioBased explicit ratio scenarios
        [TestCase(SamplerType.TraceIdRatioBased, "01", true, 1.0f, TestName = "TraceIdRatioBased_Ratio1_WithSampledTraceparent")]
        [TestCase(SamplerType.TraceIdRatioBased, "00", true, 1.0f, TestName = "TraceIdRatioBased_Ratio1_WithNotSampledTraceparent")]
        [TestCase(SamplerType.TraceIdRatioBased, "01", false, 0.0f, TestName = "TraceIdRatioBased_Ratio0_WithSampledTraceparent")]
        [TestCase(SamplerType.TraceIdRatioBased, "00", false, 0.0f, TestName = "TraceIdRatioBased_Ratio0_WithNotSampledTraceparent")]
        public void RemoteParentSamplerType_Combinations(
            SamplerType samplerType,
            string traceparentSampledFlag,
            bool expectedSampled,
            float? ratio)
        {
            // Initialize CompositeTestAgent with the specified sampler types
            _compositeTestAgent = new CompositeTestAgent(false, false,
                remoteParentSampledSamplerType: samplerType,
                remoteParentNotSampledSamplerType: samplerType);
            _agent = _compositeTestAgent.GetAgent();

            // If testing TraceIdRatioBased, inject the ratio into both sampled/not-sampled sampler configs then push config
            if (samplerType == SamplerType.TraceIdRatioBased && ratio.HasValue)
            {
                if (_compositeTestAgent.LocalConfiguration.distributedTracing.sampler.remoteParentSampled.Item is TraceIdRatioSamplerType rpSampled)
                {
                    rpSampled.sampleRatio = (decimal)ratio.Value;
                }
                if (_compositeTestAgent.LocalConfiguration.distributedTracing.sampler.remoteParentNotSampled.Item is TraceIdRatioSamplerType rpNotSampled)
                {
                    rpNotSampled.sampleRatio = (decimal)ratio.Value;
                }
                _compositeTestAgent.PushConfiguration();
            }

            // Arrange
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: "WebTransaction",
                transactionDisplayName: $"{samplerType}_Transaction",
                doNotTrackAsUnitOfWork: false);

            var headers = new Dictionary<string, string>
            {
                { "traceparent", $"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-{traceparentSampledFlag}" }
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(
                headers,
                (carrier, key) => carrier.ContainsKey(key) ? new[] { carrier[key] } : null,
                TransportType.HTTP);

            // Act
            var segment = _agent.StartTransactionSegmentOrThrow($"{samplerType}_Segment");
            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            // Assert
            var spanEvents = _compositeTestAgent.SpanEvents;
            if (samplerType == SamplerType.AlwaysOff
                ||
                (samplerType == SamplerType.TraceIdRatioBased && ratio == 0.0f))
            {
                Assert.That(spanEvents, Is.Empty);
                return;
            }

            Assert.That(spanEvents, Is.Not.Empty);
            foreach (var span in spanEvents)
            {
                var intrinsicAttributes = span.IntrinsicAttributes();
                Assert.That(intrinsicAttributes["sampled"], Is.EqualTo(expectedSampled));
            }
        }
    }
}
