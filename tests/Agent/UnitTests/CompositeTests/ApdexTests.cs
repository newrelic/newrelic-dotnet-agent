// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions;
using System.Threading;

namespace CompositeTests
{
    internal class ApdexTests
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
        public void apdexPerfZone_satisfying_if_time_is_less_than_apdexT()
        {
            // ARRANGE
            var apdexT = TimeSpan.FromMilliseconds(100);
            _compositeTestAgent.ServerConfiguration.ApdexT = apdexT.TotalSeconds;
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
            var expectedEventAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "nr.apdexPerfZone", Value = "S"}
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent);
        }

        [Test]
        public void apdexPerfZone_tolerating_if_time_is_more_than_apdexT_but_less_than_four_times_apdexT()
        {
            // ARRANGE
            var apdexT = TimeSpan.FromMilliseconds(20);
            _compositeTestAgent.ServerConfiguration.ApdexT = apdexT.TotalSeconds;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            Thread.Sleep(apdexT.Multiply(2));
            segment.End();
            tx.End();
            _compositeTestAgent.Harvest();
            // ==== ACT ====

            // ASSERT
            var expectedEventAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "nr.apdexPerfZone", Value = "T"}
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent);
        }

        [Test]
        public void apdexPerfZone_frustrating_if_time_is_more_than_four_times_apdexT()
        {
            // ARRANGE
            var apdexT = TimeSpan.FromMilliseconds(1);
            _compositeTestAgent.ServerConfiguration.ApdexT = apdexT.TotalSeconds;
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            Thread.Sleep(apdexT.Multiply(5));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();
            // ==== ACT ====

            // ASSERT
            var expectedEventAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "nr.apdexPerfZone", Value = "F"}
            };

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent);
        }

        [Test]
        public void keyTransaction_apdexPerfZone_frustrating_if_time_is_more_than_four_times_keyTransaction_apdexT()
        {
            // ARRANGE
            // config apdexT
            var serverApdexT = TimeSpan.FromMilliseconds(100);
            _compositeTestAgent.ServerConfiguration.ApdexT = serverApdexT.TotalSeconds;

            // key transaction apdexT
            var keyTransactionApdexT = TimeSpan.FromMilliseconds(1);
            _compositeTestAgent.ServerConfiguration.WebTransactionsApdex = new Dictionary<string, double> { { "WebTransaction/Action/name", keyTransactionApdexT.TotalSeconds } };

            // push the config
            _compositeTestAgent.PushConfiguration();

            // ==== ACT ====
            var tx = _agent.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = _agent.StartTransactionSegmentOrThrow("segmentName");
            Thread.Sleep(keyTransactionApdexT.Multiply(5));
            segment.End();
            tx.End();

            _compositeTestAgent.Harvest();
            // ==== ACT ====

            // ASSERT
            var expectedEventAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "nr.apdexPerfZone", Value = "F"}
            };
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            TransactionEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.Intrinsics, transactionEvent);
        }
    }
}
