// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.AgentHealth
{
    internal class FakeScheduler : IScheduler
    {
        protected Action _action;
        public void ExecuteOnce(Action action, TimeSpan timeUntilExecution)
        {
            _action = action;
        }

        public void ExecuteEvery(Action action, TimeSpan timeBetweenExecutions, TimeSpan? optionalInitialDelay = null)
        {
            _action = action;
        }

        public void StopExecuting(Action action, TimeSpan? timeToWaitForInProgressAction = null)
        {
            _action = null;
        }

        public void ForceExecute()
        {
            _action?.Invoke();
        }
    }

    [TestFixture]
    internal class AgentHealthHeartbeatTests
    {
        private static IMetricBuilder GetSimpleMetricBuilder()
        {
            var metricNameService = Mock.Create<IMetricNameService>();
            Mock.Arrange(() => metricNameService.RenameMetric(Arg.IsAny<string>())).Returns<string>(name => name);
            return new MetricWireModel.MetricBuilder(metricNameService);
        }

        [Test]
        public void HeartbeatTest()
        {
            var scheduler = new FakeScheduler();
            var reporter = new AgentHealthReporter(GetSimpleMetricBuilder(), scheduler);
            using (var logger = new TestUtilities.Logging())
            {
                // Make sure all the event types get seen in the log
                reporter.ReportTransactionEventsSent(1);
                reporter.ReportCustomEventsSent(2);
                reporter.ReportErrorEventsSent(3);
                reporter.ReportSpanEventsSent(4);
                reporter.ReportInfiniteTracingSpanEventsSent(5);
                reporter.ReportLoggingEventsSent(6);

                scheduler.ForceExecute();

                NrAssert.Multiple(
                    () => Assert.That(logger.HasMessageThatContains("1 Transaction"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("2 Custom"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("3 Error"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("4 Span"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("5 InfiniteTracingSpan"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("6 Log"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("No events"), Is.False)
                    );

                // Make sure they all update their cumulative counts
                for (int twice = 0; twice < 2; twice++)
                {
                    reporter.ReportTransactionEventsSent(1);
                    reporter.ReportCustomEventsSent(2);
                    reporter.ReportErrorEventsSent(3);
                    reporter.ReportSpanEventsSent(4);
                    reporter.ReportInfiniteTracingSpanEventsSent(5);
                    reporter.ReportLoggingEventsSent(6);
                }

                scheduler.ForceExecute();
                NrAssert.Multiple(
                    () => Assert.That(logger.HasMessageThatContains("2 Transaction"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("4 Custom"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("6 Error"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("8 Span"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("10 InfiniteTracingSpan"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("12 Log"), Is.True),
                    () => Assert.That(logger.HasMessageThatContains("No events"), Is.False)
                    );

                // Make sure they get cleared out between triggers
                scheduler.ForceExecute();

                Assert.That(logger.HasMessageThatContains("No events"), Is.True);
            }
        }
    }
}
