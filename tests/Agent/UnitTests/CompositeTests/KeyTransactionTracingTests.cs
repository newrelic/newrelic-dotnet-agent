// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace CompositeTests
{
    internal class KeyTransactionTracingTests
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

        [Test]
        public void keytransaction_trace_not_created_when_not_configured()
        {
            // ARRANGE
            var keyTransactions = new Dictionary<string, double>
            {
                { "WebTransaction/Action/other", 0.1 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            Assert.That(_compositeTestAgent.TransactionTraces, Is.Empty);
        }

        [Test]
        public void keytransaction_trace_not_created_when_configured_and_not_above_apdexT()
        {
            // ARRANGE
            var keyTransactions = new Dictionary<string, double>
            {
                { "WebTransaction/Action/name", 10.0 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            Assert.That(_compositeTestAgent.TransactionTraces, Is.Empty);
        }

        [Test]
        public void keytransaction_trace_created_when_configured_and_above_apdexT()
        {
            // ARRANGE
            var keyTransactions = new Dictionary<string, double>
            {
                { "WebTransaction/Action/name", 0.00001 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();

            Assert.That(transactionTrace.TransactionMetricName, Is.EqualTo("WebTransaction/Action/name"));
        }

        [Test]
        public void keytransaction_worst_trace_collected()
        {
            // ARRANGE
            var keyTransactions = new Dictionary<string, double>
            {
                { "WebTransaction/Action/name", 0.001 },
                { "WebTransaction/Action/name2", 0.0000001 } // will generate a higher "score"
		};
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            segment.End();
            tx.End();

            tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name2",
                doNotTrackAsUnitOfWork: true);
            segment = _agent.StartTransactionSegmentOrThrow("segmentName2");
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();

            Assert.That(transactionTrace.TransactionMetricName, Is.EqualTo("WebTransaction/Action/name2"));
        }
    }
}
