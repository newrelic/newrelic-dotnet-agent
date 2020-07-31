// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NUnit.Framework;

namespace CompositeTests
{
    internal class KeyTransactionTracingTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private IAgentWrapperApi _agentWrapperApi;

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();
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
            var keyTransactions = new ConcurrentDictionary<string, double>
            {
                { "WebTransaction/Action/other", 0.1 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
                segment.End();
            }
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            Assert.IsEmpty(_compositeTestAgent.TransactionTraces);
        }

        [Test]
        public void keytransaction_trace_not_created_when_configured_and_not_above_apdexT()
        {
            // ARRANGE
            var keyTransactions = new ConcurrentDictionary<string, double>
            {
                { "WebTransaction/Action/name", 10.0 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
                segment.End();
            }
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            Assert.IsEmpty(_compositeTestAgent.TransactionTraces);
        }

        [Test]
        public void keytransaction_trace_created_when_configured_and_above_apdexT()
        {
            // ARRANGE
            var keyTransactions = new ConcurrentDictionary<string, double>
            {
                { "WebTransaction/Action/name", 0.00001 }
            };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
                segment.End();
            }
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();

            Assert.AreEqual("WebTransaction/Action/name", transactionTrace.TransactionMetricName);
        }

        [Test]
        public void keytransaction_worst_trace_collected()
        {
            // ARRANGE
            var keyTransactions = new ConcurrentDictionary<string, double>
            {
                { "WebTransaction/Action/name", 0.001 },
                { "WebTransaction/Action/name2", 0.0000001 } // will generate a higher "score"
        };
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = keyTransactions;
            _compositeTestAgent.ServerConfiguration.ApdexT = 10.0;
            _compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = 10.0;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
            {
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
                segment.End();
            }

            using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name2"))
            {
                var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName2");
                segment.End();
            }
            _compositeTestAgent.Harvest();
            // ==== ACT ====


            // ASSERT
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();

            Assert.AreEqual("WebTransaction/Action/name2", transactionTrace.TransactionMetricName);
        }
    }
}
