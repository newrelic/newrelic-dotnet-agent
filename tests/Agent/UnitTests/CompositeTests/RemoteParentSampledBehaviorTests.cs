// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using System.Collections.Generic;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace CompositeTests
{
    public class RemoteParentSampledBehaviorTests
    {
        private CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
        }

        [TearDown]
        public void TearDown()
        {
            _compositeTestAgent?.Dispose();
        }

        [TestCase(RemoteParentSampledBehavior.AlwaysOn, "01", true, TestName = "AlwaysOn_WithSampledTraceparent")]
        [TestCase(RemoteParentSampledBehavior.AlwaysOn, "00", true, TestName = "AlwaysOn_WithNotSampledTraceparent")]
        [TestCase(RemoteParentSampledBehavior.AlwaysOff, "01", false, TestName = "AlwaysOff_WithSampledTraceparent")]
        [TestCase(RemoteParentSampledBehavior.AlwaysOff, "00", false, TestName = "AlwaysOff_WithNotSampledTraceparent")]
        [TestCase(RemoteParentSampledBehavior.Default, "01", true, TestName = "Default_WithSampledTraceparent")]
        [TestCase(RemoteParentSampledBehavior.Default, "00", true, TestName = "Default_WithNotSampledTraceparent")] // uses existing logic
        public void RemoteParentSampledBehavior_Combinations(RemoteParentSampledBehavior behavior, string traceparentSampledFlag, bool expectedSampled)
        {
            // Initialize CompositeTestAgent with the specified RemoteParentSampledBehavior
            _compositeTestAgent?.Dispose();
            _compositeTestAgent = new CompositeTestAgent(false, false,
                remoteParentSampledBehavior: behavior,
                remoteParentNotSampledBehavior: behavior);
            _agent = _compositeTestAgent.GetAgent();

            // Arrange
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: "WebTransaction",
                transactionDisplayName: $"{behavior}_Transaction",
                doNotTrackAsUnitOfWork: false);

            var headers = new Dictionary<string, string>
            {
                { "traceparent", $"00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-{traceparentSampledFlag}" },
                { "tracestate", "vendor1=value1,vendor2=value2" } // Example W3C tracestate header
            };

            _agent.CurrentTransaction.AcceptDistributedTraceHeaders(headers, (carrier, key) => carrier.ContainsKey(key) ? new[] { carrier[key] } : null, TransportType.HTTP);

            // Act
            var segment = _agent.StartTransactionSegmentOrThrow($"{behavior}_Segment");
            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            // Assert
            var spanEvents = _compositeTestAgent.SpanEvents;
            if (behavior == RemoteParentSampledBehavior.AlwaysOff)
            {
                Assert.That(spanEvents, Is.Empty);
                return;
            }
            else
            {
                Assert.That(spanEvents, Is.Not.Empty);

                foreach (var span in spanEvents)
                {
                    var intrinsicAttributes = span.IntrinsicAttributes();
                    Assert.That(intrinsicAttributes["sampled"], Is.EqualTo(expectedSampled));
                }
            }
        }
    }
}
