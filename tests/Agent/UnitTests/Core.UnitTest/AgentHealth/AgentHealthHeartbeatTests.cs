// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.WireModels;
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
        public static IMetricBuilder GetSimpleMetricBuilder()
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
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("1 Transaction")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("2 Custom")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("3 Error")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("4 Span")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("5 InfiniteTracingSpan")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("6 Log")),
                    () => ClassicAssert.IsFalse(logger.HasMessageThatContains("No events"))
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
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("2 Transaction")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("4 Custom")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("6 Error")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("8 Span")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("10 InfiniteTracingSpan")),
                    () => ClassicAssert.IsTrue(logger.HasMessageThatContains("12 Log")),
                    () => ClassicAssert.IsFalse(logger.HasMessageThatContains("No events"))
                    );

                // Make sure they get cleared out between triggers
                scheduler.ForceExecute();

                ClassicAssert.IsTrue(logger.HasMessageThatContains("No events"));
            }
        }
    }
}
