// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace CompositeTests
{
    [TestFixture]
    public class TransactionTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        #region Transaction Events Tests
        [Test]
        public void UnknownRequestUriInTransactionEvent()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            Assert.That(transactionEvent.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"], Is.EqualTo("/Unknown"));
        }

        [Test]
        public void RequestUriInTransactionEvent()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            Assert.That(transactionEvent.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"], Is.EqualTo("myuri"));
        }

        [Test]
        public void NoRequestUriInTransactionEvent()
        {
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            Assert.That(transactionEvent.GetAttributes(AttributeClassification.AgentAttributes).ContainsKey("request.uri"), Is.False);
        }
        #endregion

        #region Transaction Traces Tests
        [Test]
        public void UnknownRequestUriInTransactionTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => Assert.That(transactionTrace.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"], Is.EqualTo("/Unknown")),
                () => Assert.That(transactionTrace.Uri, Is.EqualTo("/Unknown"))
            );
        }

        [Test]
        public void RequestUriInTransactionTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => Assert.That(transactionTrace.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"], Is.EqualTo("myuri")),
                () => Assert.That(transactionTrace.Uri, Is.EqualTo("myuri"))
            );
        }

        [Test]
        public void NoRequestUriInTransactionTrace()
        {
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => Assert.That(transactionTrace.GetAttributes(AttributeClassification.AgentAttributes).ContainsKey("request.uri"), Is.False),
                () => Assert.That(transactionTrace.Uri, Is.EqualTo(null))
            );
        }
        #endregion


        #region Error Events Test
        // We always exclude the request.uri attribute when there is no transaction, regardless of the attribute inclusion/exclusion logic
        [Test]
        public void NoRequestUriAttributeInErrorEventWithoutTransaction()
        {
            AgentApi.NoticeError(new Exception("oh no"));

            _compositeTestAgent.Harvest();

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.That(errorEvent.AgentAttributes().ContainsKey("request.uri"), Is.False);
        }

        [Test]
        public void UnknownRequestUriInErrorEventWithTransaction()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            tx.NoticeError(new Exception("test exception"));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.That(errorEvent.AgentAttributes()["request.uri"], Is.EqualTo("/Unknown"));
        }

        [Test]
        public void RequestUriInErrorEventWithTransaction()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            tx.NoticeError(new Exception("test exception"));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.That(errorEvent.AgentAttributes()["request.uri"], Is.EqualTo("myuri"));
        }

        [Test]
        public void NoRequestUriInErrorEventWithTransaction()
        {
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            tx.NoticeError(new Exception("test exception"));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.That(errorEvent.AgentAttributes().ContainsKey("request.uri"), Is.False);
        }

        [Test]
        public void TransactionEventIgnoreErrorClasses()
        {
            _compositeTestAgent.LocalConfiguration.errorCollector.ignoreClasses.errorClass =
                new List<string>() { "System.OperationCanceledException" };
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(true, "TestCategory", "TestTransaction", false);
            var segment = _agent.StartCustomSegmentOrThrow("TestSegment");


            //The sleep ensures that the exception occurs after the transaction
            //start date.  Without this, there is a small chance that the timesetamps 
            //could be the same which would make it difficult to distinguish. 
            Thread.Sleep(5);

            tx.NoticeError(new System.OperationCanceledException("This exception should be ignored"));

            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var exEvents = _compositeTestAgent.ErrorEvents;

            Assert.That(exEvents, Is.Empty);
        }

        #endregion

        #region Error Traces Tests
        [Test]
        public void RequestUriInErrorTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            tx.SetUri("myuri");
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            tx.NoticeError(new Exception("test exception"));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            Assert.That(errorTrace.Attributes.AgentAttributes.Any(kv => kv.Key == "request.uri" && (string)kv.Value == "myuri"), Is.True);
        }

        [Test]
        public void NoRequestUriInErrorTrace()
        {
            _compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            tx.NoticeError(new Exception("test exception"));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            Assert.That(errorTrace.Attributes.AgentAttributes.Any(kv => kv.Key == "request.uri"), Is.False);
        }

        [Test]
        public void TransactionEventAndErrorEventUseCorrectTimestampAttributes()
        {
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: false,
                category: "TestCategory",
                transactionDisplayName: "TestTransaction",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("TestSegment");

            //The sleep ensures that the exception occurs after the transaction
            //start date.  Without this, there is a small chance that the timesetamps 
            //could be the same which would make it difficult to distinguish. 
            Thread.Sleep(5);

            tx.NoticeError(new Exception("This is a test exception"));

            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var txEvents = _compositeTestAgent.TransactionEvents;
            var exEvents = _compositeTestAgent.ErrorEvents;

            Assert.Multiple(() =>
            {
                Assert.That(txEvents, Has.Count.EqualTo(1));
                Assert.That(exEvents, Has.Count.EqualTo(1));
            });

            var txEvent = txEvents.First();
            var exEvent = exEvents.First();

            Assert.Multiple(() =>
            {
                Assert.That(txEvent.IntrinsicAttributes().ContainsKey("timestamp"), Is.True);
                Assert.That(exEvent.IntrinsicAttributes().ContainsKey("timestamp"), Is.True);
            });

            var txEventTimeStamp = (long)txEvent.IntrinsicAttributes()["timestamp"];
            var exEventTimeStamp = (long)exEvent.IntrinsicAttributes()["timestamp"];

            Assert.That(txEventTimeStamp, Is.LessThan(exEventTimeStamp));
        }

        #endregion

        [Test]
        public void SimpleTransaction_CreatesTransactionTraceAndEvent()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
            NrAssert.Multiple(
                () => Assert.That(transactionTrace.TransactionMetricName, Is.EqualTo("WebTransaction/Action/name")),

                () => Assert.That(transactionEvent.IntrinsicAttributes()["name"], Is.EqualTo("WebTransaction/Action/name")),
                () => Assert.That(transactionEvent.IntrinsicAttributes()["type"], Is.EqualTo("Transaction"))
                );
        }

        [Test]
        public void FastTransaction_DoesNotCreateTransactionTrace()
        {
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = TimeSpan.FromSeconds(5);
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            _agent.StartTransactionSegmentOrThrow("segmentName").End();
            tx.End();

            _compositeTestAgent.Harvest();

            NrAssert.Multiple(
                () => Assert.That(_compositeTestAgent.TransactionTraces, Is.Empty),
                () => Assert.That(_compositeTestAgent.TransactionEvents, Has.Count.EqualTo(1))
                );
        }

        [Test]
        public void TransactionWithUnfinishedSegments_CreatesTraceAndEvent()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            _agent.StartTransactionSegmentOrThrow("segmentName");

            // Finish the transaction without ending its unfinished segment
            tx.End();

            _compositeTestAgent.Harvest();

            NrAssert.Multiple(
                () => Assert.That(_compositeTestAgent.TransactionTraces.Any(), Is.True),
                () => Assert.That(_compositeTestAgent.TransactionEvents.Any(), Is.True)
                );
        }

        [Test]
        public void TransactionWithNoSegments_DoesNotCreateTraceOrEvent()
        {
            _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true).End();

            _compositeTestAgent.Harvest();

            NrAssert.Multiple(
                () => Assert.That(_compositeTestAgent.TransactionTraces.Any(), Is.False),
                () => Assert.That(_compositeTestAgent.TransactionEvents.Any(), Is.False)
                );
        }

        [Test]
        public void ErrorTransaction_CreatesErrorTraceAndEvent()
        {
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");

            transaction.NoticeError(new Exception("Oh no!"));
            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
            var errorTrace = _compositeTestAgent.ErrorTraces.FirstOrDefault();
            NrAssert.Multiple(
                () => Assert.That(transactionEvent.IntrinsicAttributes()["errorType"], Is.EqualTo("System.Exception")),
                () => Assert.That(transactionEvent.IntrinsicAttributes()["errorMessage"], Is.EqualTo("Oh no!")),
                () => Assert.That(errorTrace.Path, Is.EqualTo("WebTransaction/Action/rootSegmentMetricName")),
                () => Assert.That(errorTrace.ExceptionClassName, Is.EqualTo("System.Exception")),
                () => Assert.That(errorTrace.Message, Is.EqualTo("Oh no!"))
                );
        }

        [Test]
        public void AgentTiming_WhenDisabledThenNoAgentTimingMetrics()
        {
            _compositeTestAgent.LocalConfiguration.diagnostics.captureAgentTiming = false;
            _compositeTestAgent.PushConfiguration();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();
            var metrics = _compositeTestAgent.Metrics.Where(x => x.MetricNameModel.Name.Contains("AgentTiming"));
            Assert.That(metrics, Is.Empty);
        }

        [Test]
        public void AgentTiming_WhenEnabledThenAgentTimingMetrics()
        {
            _compositeTestAgent.LocalConfiguration.diagnostics.captureAgentTiming = true;
            _compositeTestAgent.PushConfiguration();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();
            var metrics = _compositeTestAgent.Metrics.Where(x => x.MetricNameModel.Name.Contains("AgentTiming"));
            Assert.That(metrics, Is.Not.Empty);
        }

        [Test]
        public void ResponseTimeShouldMatchTransactionDurationForWebTransactions()
        {
            //Setup a transaction where the response time and transaction duration are about the same
            var upperBoundStopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            var lowerBoundStopWatch = Stopwatch.StartNew();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            segment.End();
            lowerBoundStopWatch.Stop();
            transaction.End();
            upperBoundStopWatch.Stop();

            _compositeTestAgent.Harvest();

            //Use the WebTransaction metric which should contain the response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "WebTransaction");
            NrAssert.Multiple(
                    () => Assert.That(timingMetric.DataModel.Value1, Is.GreaterThanOrEqualTo(lowerBoundStopWatch.Elapsed.TotalSeconds)),
                    () => Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(upperBoundStopWatch.Elapsed.TotalSeconds))
                );
        }

        [Test]
        public void ResponseTimeShouldMatchTransactionDurationForOtherTransactions()
        {
            //Setup a transaction where the response time and transaction duration are about the same
            var upperBoundStopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: false,
                category: "category",
                transactionDisplayName: "transactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            var lowerBoundStopWatch = Stopwatch.StartNew();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            segment.End();
            lowerBoundStopWatch.Stop();
            transaction.End();
            upperBoundStopWatch.Stop();

            _compositeTestAgent.Harvest();

            //Use the OtherTransaction/all metric which should contain the duration instead of response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "OtherTransaction/all");
            NrAssert.Multiple(
                    () => Assert.That(timingMetric.DataModel.Value1, Is.GreaterThanOrEqualTo(lowerBoundStopWatch.Elapsed.TotalSeconds)),
                    () => Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(upperBoundStopWatch.Elapsed.TotalSeconds))
                );
        }

        [Test]
        public void ResponseTimeAndDurationAreNotTheSameForWebTransactions()
        {
            //Setup a transaction where the response time and transaction duration are different
            var stopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            transaction.Hold();
            transaction.End();
            var expectedResponseTimeUpperBound = stopWatch.Elapsed.TotalSeconds;

            //Cause a delay so that response time and duration should be very different
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            segment.End();
            transaction.Release();
            stopWatch.Stop();

            _compositeTestAgent.Harvest();

            //Use the WebTransaction metric which should contain the response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "WebTransaction");
            Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(expectedResponseTimeUpperBound));
        }

        [Test]
        public void ResponseTimeAndDurationAreNotTheSameForOtherTransactions()
        {
            //Setup a transaction where the response time and transaction duration are different
            var stopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: false,
                category: "category",
                transactionDisplayName: "transactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            transaction.Hold();
            transaction.End();
            var expectedResponseTimeUpperBound = stopWatch.Elapsed.TotalSeconds;

            //Cause a delay so that response time and duration should be very different
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            segment.End();
            transaction.Release();
            stopWatch.Stop();
            var expectedDurationUpperBound = stopWatch.Elapsed.TotalSeconds;

            _compositeTestAgent.Harvest();

            //Use the OtherTransaction/all metric which should contain the duration instead of response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "OtherTransaction/all");
            NrAssert.Multiple(
                    () => Assert.That(timingMetric.DataModel.Value1, Is.GreaterThan(expectedResponseTimeUpperBound)),
                    () => Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(expectedDurationUpperBound))
                );
        }

        [Test]
        public void ResponseTimeShouldNotBeCapturedWhenReleasingATransactionBeforeItEnds()
        {
            //Setup a transaction where the response time and transaction duration are different
            var upperBoundStopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "rootSegmentMetricName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            var lowerBoundStopWatch = Stopwatch.StartNew();
            transaction.Hold();
            segment.End();
            transaction.Release();
            //Cause a delay so that we can clearly see a difference between the time when Release and End are called
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            lowerBoundStopWatch.Stop();
            transaction.End();
            upperBoundStopWatch.Stop();

            _compositeTestAgent.Harvest();

            //Use the WebTransaction metric which should contain the response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "WebTransaction");
            NrAssert.Multiple(
                    () => Assert.That(timingMetric.DataModel.Value1, Is.GreaterThanOrEqualTo(lowerBoundStopWatch.Elapsed.TotalSeconds)),
                    () => Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(upperBoundStopWatch.Elapsed.TotalSeconds))
                );
        }

        [Test]
        public void ResponseTimeShouldOnlyBeCapturedOnce()
        {
            var upperBoundStopWatch = Stopwatch.StartNew();
            var transaction = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Custom),
                transactionDisplayName: "CustomWebTransaction",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            transaction.End();
            upperBoundStopWatch.Stop();
            //Cause a delay so that we can clearly see a difference between the 2 calls to End the transaction.
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            transaction.End();

            _compositeTestAgent.Harvest();

            //Use the WebTransaction metric which should contain the response time
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "WebTransaction");
            Assert.That(timingMetric.DataModel.Value1, Is.LessThanOrEqualTo(upperBoundStopWatch.Elapsed.TotalSeconds));
        }

        [Test]
        public void TransactionShouldBeRemovedFromContextStorageWhenResponseTimeIsCaptured()
        {
            // Setup a transaction where fire and forget async work is tracked
            var transaction = _agent.CreateTransaction(
                isWeb: false,
                category: "category",
                transactionDisplayName: "transactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("fireAndForgetSegment");
            transaction.Hold();
            // Response time should be tracked and the current transaction removed from storage.
            transaction.End();

            var transactionAfterCallToEnd = _agent.CurrentTransaction;

            // The fire and forget segment ends and then releases the transaction
            segment.End();
            transaction.Release();

            _compositeTestAgent.Harvest();

            //Use the OtherTransaction/all metric to confirm that the transaction was transformed and harvested
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "OtherTransaction/all");
            NrAssert.Multiple(
                    () => Assert.That(transactionAfterCallToEnd.IsValid, Is.False, "The current transaction should be the NoOpTransaction."),
                    () => Assert.That(timingMetric.DataModel.Value0, Is.EqualTo(1.0), "The transaction should be harvested.")
                );
        }

        [Test]
        public void TransactionShouldNotBeRemovedFromContextStorageWhenResponseTimeIsNotCaptured()
        {
            // Setup a transaction where fire and forget async work is tracked
            var transaction = _agent.CreateTransaction(
                isWeb: false,
                category: "category",
                transactionDisplayName: "transactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("fireAndForgetSegment");
            transaction.Hold();
            // Response time should be tracked and the current transaction removed from storage.
            transaction.End(captureResponseTime: false);

            var transactionAfterCallToEnd = _agent.CurrentTransaction;

            // The fire and forget segment ends and then releases the transaction
            segment.End();
            transaction.Release();

            _compositeTestAgent.Harvest();

            //Use the OtherTransaction/all metric to confirm that the transaction was transformed and harvested
            var timingMetric = _compositeTestAgent.Metrics.First(x => x.MetricNameModel.Name == "OtherTransaction/all");
            NrAssert.Multiple(
                    () => Assert.That(transactionAfterCallToEnd.IsValid, Is.True, "The current transaction should not be the NoOpTransaction."),
                    () => Assert.That(timingMetric.DataModel.Value0, Is.EqualTo(1.0), "The transaction should be harvested.")
                );
        }
    }
}
