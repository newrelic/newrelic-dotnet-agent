// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CompositeTests
{
    [TestFixture]
    public class StackTraceTransactionTraceTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgent _agent;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
            _compositeTestAgent.LocalConfiguration.transactionTracer.maxStackTrace = 1; // anything over 0 will work
            _compositeTestAgent.PushConfiguration();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [Test]
        public void TransactionTrace_MethodSegment_DefaultConfig_NoStackTrace()
        {
            _compositeTestAgent.LocalConfiguration.transactionTracer.maxStackTrace = 0; // the default
            _compositeTestAgent.PushConfiguration();

            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMethodSegmentOrThrow("mytype", "mymethod");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            Assert.That(parameters.Keys, Does.Not.Contain("backtrace"));
        }

        [Test]
        public void TransactionTrace_MethodSegment_StackTracesEnabled_HasStackTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMethodSegmentOrThrow("mytype", "mymethod");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace is List<string>, Is.True);
        }

        [Test]
        public void TransactionTrace_MethodSegment_StackTracesEnabled_HastStackFrames()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMethodSegmentOrThrow("mytype", "mymethod");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace, Has.Count.GreaterThan(2)); // should be around 15
        }

        [Test]
        public void TransactionTrace_DatastoreRequestSegment_StackTracesEnabled_HasStackTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var queryParameters = new Dictionary<string, IConvertible>
            {
                {"myKey1", "myValue1"},
                {"myKey2", "myValue2"}
            };
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace is List<string>, Is.True);
        }

        [Test]
        public void TransactionTrace_DatastoreRequestSegment_StackTracesEnabled_HastStackFrames()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var queryParameters = new Dictionary<string, IConvertible>
            {
                {"myKey1", "myValue1"},
                {"myKey2", "myValue2"}
            };
            var segment = _agent.StartDatastoreRequestSegmentOrThrow("SELECT", DatastoreVendor.MSSQL, "Table1", "SELECT * FROM Table1", queryParameters: queryParameters);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace, Has.Count.GreaterThan(2)); // should be around 15
        }

        [Test]
        public void TransactionTrace_ExternalRequestSegment_StackTracesEnabled_HasStackTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://www.newrelic.com"), "GET");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace is List<string>, Is.True);
        }

        [Test]
        public void TransactionTrace_ExternalRequestSegment_StackTracesEnabled_HastStackFrames()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartExternalRequestSegmentOrThrow(new Uri("http://www.newrelic.com"), "GET");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace, Has.Count.GreaterThan(2)); // should be around 15
        }

        [Test]
        public void TransactionTrace_MessageBrokerSegment_StackTracesEnabled_HasStackTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMessageBrokerSegmentOrThrow("MSMQ", MessageBrokerDestinationType.Queue, "aplace", MessageBrokerAction.Consume);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace is List<string>, Is.True);
        }

        [Test]
        public void TransactionTrace_MessageBrokerSegment_StackTracesEnabled_HastStackFrames()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartMessageBrokerSegmentOrThrow("MSMQ", MessageBrokerDestinationType.Queue, "aplace", MessageBrokerAction.Consume);
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace, Has.Count.GreaterThan(2)); // should be around 15
        }

        [Test]
        public void TransactionTrace_CustomSegment_StackTracesEnabled_HasStackTrace()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("segmentname");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace is List<string>, Is.True);
        }

        [Test]
        public void TransactionTrace_CustomSegment_StackTracesEnabled_HastStackFrames()
        {
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartCustomSegmentOrThrow("segmentname");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();

            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var parameters = transactionTrace.TransactionTraceData.RootSegment.Children[0].Children[0].Parameters;

            var stackTrace = parameters["backtrace"] as List<string>;
            Assert.That(stackTrace, Has.Count.GreaterThan(2)); // should be around 15
        }
    }
}
