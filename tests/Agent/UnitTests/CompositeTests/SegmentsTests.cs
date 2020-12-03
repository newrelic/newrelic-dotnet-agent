// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.WireModels;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompositeTests
{
    [TestFixture]
    public class SegmentsTests
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

        #region Segment nesting tests

        [Test]
        public void SingleAddedSegment_IsNestedBelowRootSegment()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("childSegment");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var rootSegment = GetFirstSegmentOrThrow();
            NrAssert.Multiple(
                () => Assert.AreEqual(1, rootSegment.Children.Count),

                () => Assert.AreEqual("childSegment", rootSegment.Children[0].Name),
                () => Assert.AreEqual(0, rootSegment.Children[0].Children.Count)
                );
        }

        [Test]
        public void SiblingSegments_AreNestedTogether()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("childSegment1");
            segment.End();
            segment = _agent.StartTransactionSegmentOrThrow("childSegment2");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var rootSegment = GetFirstSegmentOrThrow();
            NrAssert.Multiple(
                () => Assert.AreEqual(2, rootSegment.Children.Count),

                () => Assert.AreEqual("childSegment1", rootSegment.Children[0].Name),
                () => Assert.AreEqual(0, rootSegment.Children[0].Children.Count),

                () => Assert.AreEqual("childSegment2", rootSegment.Children[1].Name),
                () => Assert.AreEqual(0, rootSegment.Children[1].Children.Count)
                );
        }

        [Test]
        public void UnfinishedSegments_AreStillReported()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment1 = _agent.StartTransactionSegmentOrThrow("segmentName1");
            segment1.End();

            _agent.StartTransactionSegmentOrThrow("segmentName2");

            // Finish the transaction without ending segmentName2
            tx.End();

            _compositeTestAgent.Harvest();

            var rootSegment = GetFirstSegmentOrThrow();
            Assert.AreEqual(2, rootSegment.Children.Count);
            var finishedSegment = rootSegment.Children.ElementAt(0);
            var unfinishedSegment = rootSegment.Children.ElementAt(1);
            NrAssert.Multiple(
                () => Assert.AreEqual("segmentName1", finishedSegment.Name),
                () => Assert.IsFalse(finishedSegment.Parameters.ContainsKey("unfinished")),

                () => Assert.AreEqual("segmentName2", unfinishedSegment.Name),
                () => Assert.IsTrue(unfinishedSegment.Parameters.ContainsKey("unfinished"))
                );
        }

        #endregion Segment nesting tests

        #region Ending segments under unusual conditions tests

        [Test]
        public void EndingASegmentTwice_HasNoEffect()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("childSegment");
            segment.End();
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var rootSegment = GetFirstSegmentOrThrow();
            NrAssert.Multiple(
                () => Assert.AreEqual(1, rootSegment.Children.Count),

                () => Assert.AreEqual("childSegment", rootSegment.Children[0].Name),
                () => Assert.AreEqual(0, rootSegment.Children[0].Children.Count)
                );
        }

        [Test]
        public void EndingSegmentsInWrongOrder_DoesNotThrow()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment1 = _agent.StartTransactionSegmentOrThrow("childSegment1");
            var segment2 = _agent.StartTransactionSegmentOrThrow("childSegment2");
            segment1.End();
            segment2.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var rootSegment = GetFirstSegmentOrThrow();
            NrAssert.Multiple(
                () => Assert.AreEqual(1, rootSegment.Children.Count),

                () => Assert.AreEqual("childSegment1", rootSegment.Children[0].Name),
                () => Assert.AreEqual(1, rootSegment.Children[0].Children.Count),

                () => Assert.AreEqual("childSegment2", rootSegment.Children[0].Children[0].Name),
                () => Assert.AreEqual(0, rootSegment.Children[0].Children[0].Children.Count)
                );
        }

        #endregion Ending segments under unusual conditions tests

        #region Segment metrics and trace names

        [Test]
        public void SimpleSegment_HasCorrectTraceNameAndMetrics()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("simpleName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "DotNet/simpleName"},
                new ExpectedMetric {Name = "DotNet/simpleName", Scope = "WebTransaction/Action/name"}
            };
            var expectedSegments = new[]
            {
                "simpleName"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void SimpleSegment_ShouldNotHaveMetrics_OutsideATransaction()
        {
            var segment = _agent.StartTransactionSegmentOrThrow("simpleName");
            segment.End();

            _compositeTestAgent.Harvest();

            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "DotNet/simpleName"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }

        [Test]
        public void MethodSegment_HasCorrectTraceNameAndMetrics()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMethodSegmentOrThrow("typeName", "methodName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "DotNet/typeName/methodName"},
                new ExpectedMetric {Name = "DotNet/typeName/methodName", Scope = "WebTransaction/Action/name"}
            };
            var expectedSegments = new[]
            {
                "DotNet/typeName/methodName"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void MethodSegment_ShouldNotHaveMetrics_OutsideATransaction()
        {
            var segment = _agent.StartMethodSegmentOrThrow("typeName", "methodName");
            segment.End();

            _compositeTestAgent.Harvest();

            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "DotNet/typeName/methodName"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }

        [Test]
        public void CustomSegment_HasCorrectTraceNameAndMetrics()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("customName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Custom/customName"},
                new ExpectedMetric {Name = "Custom/customName", Scope = "WebTransaction/Action/name"}
            };
            var expectedSegments = new[]
            {
                "customName"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void CustomSegment_ShouldNotHaveMetrics_OutsideATransaction()
        {
            var segment = _agent.StartCustomSegmentOrThrow("customName");
            segment.End();

            _compositeTestAgent.Harvest();

            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Custom/customName"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }

        [Test]
        public void CustomSegment_HasCorrectMetrics_IfInputIsPrefixedWithCustom()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("Custom/customName");
            segment.End();
            tx.End();


            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
				// The agent should de-duplicate the "Custom/" prefix that was passed in
				new ExpectedMetric {Name = "Custom/customName"},
                new ExpectedMetric {Name = "Custom/customName", Scope = "WebTransaction/Action/name"}
            };

            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
        }

        [Test]
        public void ExternalSegment_HasCorrectTraceNameAndMetrics()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://www.newrelic.com/test"), "POST");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "External/all"},
                new ExpectedMetric {Name = "External/allWeb"},
                new ExpectedMetric {Name = "External/www.newrelic.com/all"},
                new ExpectedMetric {Name = "External/www.newrelic.com/Stream/POST"},
                new ExpectedMetric {Name = "External/www.newrelic.com/Stream/POST", Scope = "WebTransaction/Action/name"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "External/allOther"}
            };
            var expectedSegments = new[]
            {
                "External/www.newrelic.com/Stream/POST"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void ExternalSegment_HasCorrectTraceNameAndMetrics_IfCatResponseReceived()
        {
            const string encodingKey = "foo";
            _compositeTestAgent.ServerConfiguration.EncodingKey = encodingKey;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://www.newrelic.com/test"), "POST");

            var catResponseData = new CrossApplicationResponseData("123#456", "transactionName", 1.1f, 2.2f, 3, "guid");
            var responseHeaders = new Dictionary<string, string>
            {
                {"X-NewRelic-App-Data", HeaderEncoder.EncodeSerializedData(JsonConvert.SerializeObject(catResponseData), encodingKey)}
            };
            tx.ProcessInboundResponse(responseHeaders, segment);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "External/all"},
                new ExpectedMetric {Name = "External/allWeb"},
                new ExpectedMetric {Name = "External/www.newrelic.com/all"},
                new ExpectedMetric {Name = "External/www.newrelic.com/Stream/POST"},
                new ExpectedMetric {Name = "ExternalApp/www.newrelic.com/123#456/all"},
                new ExpectedMetric {Name = "ExternalTransaction/www.newrelic.com/123#456/transactionName"},
                new ExpectedMetric {Name = "ExternalTransaction/www.newrelic.com/123#456/transactionName", Scope = "WebTransaction/Action/name"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "External/allOther"},
                new ExpectedMetric {Name = "External/www.newrelic.com/Stream/POST", Scope = "WebTransaction/Action/name"}
            };
            var unexpectedSegments = new[]
            {
                "External/www.newrelic.com/Stream/POST"
            };
            var expectedSegments = new[]
            {
                "ExternalTransaction/www.newrelic.com/123#456/transactionName"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => TransactionTraceAssertions.SegmentsDoNotExist(unexpectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void ExternalSegment_ShouldNotHaveMetrics_OutsideATransaction()
        {
            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://www.newrelic.com/test"), "POST");
            segment.End();

            _compositeTestAgent.Harvest();

            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "External/all"},
                new ExpectedMetric {Name = "External/www.newrelic.com/all"},
                new ExpectedMetric {Name = "External/www.newrelic.com/Stream/POST"},
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }

        [Test]
        public void DatastoreSegment_HasCorrectTraceNameAndMetrics()
        {
            _compositeTestAgent.LocalConfiguration.datastoreTracer.instanceReporting.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("INSERT", DatastoreVendor.MSSQL, "MyAwesomeTable", null, null, "HostName", "1433", "MyDatabase");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/all"},
                new ExpectedMetric {Name = "Datastore/allWeb"},
                new ExpectedMetric {Name = "Datastore/MSSQL/all"},
                new ExpectedMetric {Name = "Datastore/MSSQL/allWeb"},
                new ExpectedMetric {Name = "Datastore/statement/MSSQL/MyAwesomeTable/INSERT"},
                new ExpectedMetric {Name = "Datastore/statement/MSSQL/MyAwesomeTable/INSERT", Scope = "WebTransaction/Action/name"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/INSERT"},
                new ExpectedMetric {Name = "Datastore/instance/MSSQL/HostName/1433"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/allOther"}
            };
            var expectedSegments = new[]
            {
                "Datastore/statement/MSSQL/MyAwesomeTable/INSERT"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void DatastoreSegment_HasNoInstanceMetric_WhenInstanceReportingIsDisabled()
        {
            _compositeTestAgent.LocalConfiguration.datastoreTracer.instanceReporting.enabled = false;
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("INSERT", DatastoreVendor.MSSQL, "MyAwesomeTable", null, null, "HostName", "1433", "MyDatabase");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/all"},
                new ExpectedMetric {Name = "Datastore/allWeb"},
                new ExpectedMetric {Name = "Datastore/MSSQL/all"},
                new ExpectedMetric {Name = "Datastore/MSSQL/allWeb"},
                new ExpectedMetric {Name = "Datastore/statement/MSSQL/MyAwesomeTable/INSERT"},
                new ExpectedMetric {Name = "Datastore/statement/MSSQL/MyAwesomeTable/INSERT", Scope = "WebTransaction/Action/name"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/INSERT"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/allOther"},
                new ExpectedMetric {Name = "Datastore/instance/MSSQL/HostName/1433"}
            };
            var expectedSegments = new[]
            {
                "Datastore/statement/MSSQL/MyAwesomeTable/INSERT"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }


        [Test]
        public void DatastoreSegment_HasCorrectTraceNameAndMetrics_WhenNullModel()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("INSERT", DatastoreVendor.MSSQL, null);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/all"},
                new ExpectedMetric {Name = "Datastore/allWeb"},
                new ExpectedMetric {Name = "Datastore/MSSQL/all"},
                new ExpectedMetric {Name = "Datastore/MSSQL/allWeb"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/INSERT", Scope = "WebTransaction/Action/name"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/INSERT"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/allOther"}
            };
            var expectedSegments = new[]
            {
                "Datastore/operation/MSSQL/INSERT"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void DatastoreSegment_HasCorrectTraceNameAndMetrics_WhenNullModelAndOperation()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartDatastoreRequestSegmentOrThrow(null, DatastoreVendor.MSSQL, null);

            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/all"},
                new ExpectedMetric {Name = "Datastore/allWeb"},
                new ExpectedMetric {Name = "Datastore/MSSQL/all"},
                new ExpectedMetric {Name = "Datastore/MSSQL/allWeb"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/other", Scope = "WebTransaction/Action/name"},
                new ExpectedMetric {Name = "Datastore/operation/MSSQL/other"}
            };
            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "Datastore/allOther"}
            };
            var expectedSegments = new[]
            {
                "Datastore/operation/MSSQL/other"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
                () => MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void MessageBrokerSegment_HasCorrectTraceNameAndMetrics()
        {
            var tx = _agent.CreateTransaction(
                destinationType: MessageBrokerDestinationType.Queue,
                brokerVendorName: "vendor1",
                destination: "queueA");
            var segment = _agent.StartMessageBrokerSegmentOrThrow("vendor1", MessageBrokerDestinationType.Queue, "queueA",
                MessageBrokerAction.Consume);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var expectedMetrics = new[]
            {
                new ExpectedMetric {Name = "MessageBroker/vendor1/Queue/Consume/Named/queueA"},
                new ExpectedMetric {Name = "OtherTransaction/Message/vendor1/Queue/Named/queueA" },
                new ExpectedMetric {Name = "OtherTransactionTotalTime/Message/vendor1/Queue/Named/queueA" },
                new ExpectedMetric {Name = "MessageBroker/vendor1/Queue/Consume/Named/queueA", Scope = "OtherTransaction/Message/vendor1/Queue/Named/queueA"}

            };
            var expectedSegments = new[]
            {
                 "MessageBroker/vendor1/Queue/Consume/Named/queueA"
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            NrAssert.Multiple(
                () => TransactionTraceAssertions.SegmentsExist(expectedSegments, transactionTrace),
                () => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics)
                );
        }

        [Test]
        public void MessageBrokerSegment_ShouldNotHaveMetrics_OutsideATransaction()
        {
            var segment = _agent.StartMessageBrokerSegmentOrThrow("vendor1", MessageBrokerDestinationType.Queue, "queueA",
                MessageBrokerAction.Consume);
            segment.End();

            _compositeTestAgent.Harvest();

            var unexpectedMetrics = new[]
            {
                new ExpectedMetric {Name = "MessageBroker/vendor1/Queue/Consume/Named/queueA"}
            };
            var actualMetrics = _compositeTestAgent.Metrics.ToList();
            MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
        }


        #endregion

        [Test]
        public void ChildDurationShouldNotCountTowardsParentsExclusiveTime()
        {
            var tx = _agent.CreateTransaction(
                isWeb: false,
                category: "testing",
                transactionDisplayName: "test",
                doNotTrackAsUnitOfWork: true);
            var segment = (Segment)_agent.StartCustomSegmentOrThrow("parentSegment");
            //We need the child segment to run on a different thread than the parent
            Task.Run(() =>
            {
                var childSegment = _agent.StartCustomSegmentOrThrow("childSegment");
                childSegment.DurationShouldBeDeductedFromParent = true;
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                childSegment.End();
            }).Wait();

            segment.End();
            tx.End();

            Assert.Less(segment.ExclusiveDurationOrZero, TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public void ChildDurationShouldCountTowardsParentsExclusiveTime()
        {
            var tx = _agent.CreateTransaction(
                isWeb: false,
                category: "testing",
                transactionDisplayName: "test",
                doNotTrackAsUnitOfWork: true);
            var segment = (Segment)_agent.StartCustomSegmentOrThrow("parentSegment");
            //We need the child segment to run on a different thread than the parent
            Task.Run(() =>
            {
                var childSegment = _agent.StartCustomSegmentOrThrow("childSegment");
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                childSegment.End();
            }).Wait();

            segment.End();
            tx.End();

            Assert.Greater(segment.ExclusiveDurationOrZero, TimeSpan.FromMilliseconds(100));
        }

        [Test]
        public void ChildDurationShouldNotCountTowardsParentsExclusiveTimeIfDeductDurationTrue()
        {
            var childSegmentDuration = TimeSpan.FromMilliseconds(100);

            var tx = _agent.CreateTransaction(
                isWeb: false,
                category: "testing",
                transactionDisplayName: "test",
                doNotTrackAsUnitOfWork: true);
            var segment = (Segment)_agent.StartCustomSegmentOrThrow("parentSegment");

            segment.AlwaysDeductChildDuration = true;

            //We need the child segment to run on a different thread than the parent
            Task.Run(() =>
            {
                var childSegment = _agent.StartCustomSegmentOrThrow("childSegment");
                Thread.Sleep(TimeSpan.FromMilliseconds(100));
                childSegment.End();
            }).Wait();

            segment.End();
            tx.End();

            Assert.Less(segment.ExclusiveDurationOrZero, childSegmentDuration);
        }

        [Test]
        public void SegmentEndWithExceptionCapturesErrorAttributes()
        {
            var tx = _agent.CreateTransaction(
                isWeb: false,
                category: "testing",
                transactionDisplayName: "test",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("parentSegment");

            segment.End(new Exception("Unhandled exception"));
            tx.End();

            _compositeTestAgent.Harvest();

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = "Unhandled exception" },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        #region Helper methods

        private static TransactionTraceSegment GetFirstSegmentOrThrow()
        {
            var trace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            if (trace == null)
                throw new NullReferenceException("transactionTrace");

            // The root segment will always be named ROOT, per the transaction trace spec. We then inject a synthetic "second root" segment which is always named "Transaction", in order deal with a UI bug that can't deal with multiple top-level segments.
            var rootSegment = trace.TransactionTraceData.RootSegment.Children[0];
            if (rootSegment == null)
                throw new NullReferenceException("rootSegment");

            return rootSegment;
        }

        #endregion Helper methods
    }
}
