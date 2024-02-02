// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
    [TestFixture]
    public class TransactionFinalizerTests
    {
        private TransactionFinalizer _transactionFinalizer;

        private IAgentHealthReporter _agentHealthReporter;

        private ITransactionMetricNameMaker _transactionMetricNameMaker;

        private IPathHashMaker _pathHashMaker;

        private ITransactionTransformer _transactionTransformer;
        private IAttributeDefinitionService _attribDefSvc;
        private IAttributeDefinitions _attribDefs => _attribDefSvc.AttributeDefs;


        [SetUp]
        public void SetUp()
        {
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();
            _transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
            _pathHashMaker = Mock.Create<IPathHashMaker>();
            _transactionTransformer = Mock.Create<ITransactionTransformer>();
            _attribDefSvc = new AttributeDefinitionService((f) => new AttributeDefinitions(f));
            _transactionFinalizer = new TransactionFinalizer(_agentHealthReporter, _transactionMetricNameMaker, _pathHashMaker, _transactionTransformer);
        }

        [TearDown]
        public void TearDown()
        {
            _attribDefSvc.Dispose();
            _transactionFinalizer.Dispose();
        }

        #region Finish

        [Test]
        public void Finish_UpdatesTransactionPathHash()
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            var transactionName = TransactionName.ForWebTransaction("a", "b");
            Mock.Arrange(() => transaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => transaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

            _transactionFinalizer.Finish(transaction);

            Mock.Assert(() => transaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
        }

        [Test]
        public void Finish_CallsTransactionFinish()
        {
            var transaction = Mock.Create<IInternalTransaction>();

            _transactionFinalizer.Finish(transaction);

            Mock.Assert(() => transaction.Finish());
        }

        #endregion Finish

        #region OnTransactionFinalized

        [Test]
        public void OnTransactionFinalized_CallsForceChangeDurationWith1Millisecond_IfNoSegments()
        {
            var transaction = BuildTestTransaction();
            var internalTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => internalTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(internalTransaction));

            Mock.Assert(() => internalTransaction.ForceChangeDuration(TimeSpan.FromMilliseconds(1)));
        }

        [Test]
        public void OnTransactionFinalized_CallsForceChangeDurationWithLatestStartTime_IfOnlyUnfinishedSegments()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetUnfinishedSegment(startTime, startTime.AddSeconds(0)),
                GetUnfinishedSegment(startTime, startTime.AddSeconds(1)),
                GetUnfinishedSegment(startTime, startTime.AddSeconds(2)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => mockedTransaction.ForceChangeDuration(TimeSpan.FromSeconds(2)));
        }

        [Test]
        public void OnTransactionFinalized_CallsForceChangeDurationWithLatestEndTime_IfOnlyFinishedSegments()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => mockedTransaction.ForceChangeDuration(TimeSpan.FromSeconds(3)));
        }

        [Test]
        public void OnTransactionFinalized_CallsForceChangeDurationWithLatestTime_IfMixOfFinishedAndUnfinishedSegments()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetUnfinishedSegment(startTime, startTime.AddSeconds(5)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => mockedTransaction.ForceChangeDuration(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void OnTransactionFinalized_UpdatesTransactionPathHash()
        {
            var transaction = BuildTestTransaction();
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            var transactionName = TransactionName.ForWebTransaction("a", "b");
            Mock.Arrange(() => mockedTransaction.CandidateTransactionName.CurrentTransactionName).Returns(transactionName);
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(transactionName)).Returns(new TransactionMetricName("c", "d"));
            Mock.Arrange(() => mockedTransaction.TransactionMetadata.CrossApplicationReferrerPathHash).Returns("referrerPathHash");
            Mock.Arrange(() => _pathHashMaker.CalculatePathHash("c/d", "referrerPathHash")).Returns("pathHash");

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => mockedTransaction.TransactionMetadata.SetCrossApplicationPathHash("pathHash"));
        }

        [Test]
        public void OnTransactionFinalized_CallsTransactionFinish()
        {
            var transaction = BuildTestTransaction();
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => mockedTransaction.Finish());
        }

        [Test]
        public void OnTransactionFinalized_CallsAgentHealthReporter()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            var transactionMetricName = new TransactionMetricName("c", "d");
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, Arg.IsAny<string>(), Arg.IsAny<string>()));
        }

        [Test]
        public void OnTransactionFinalized_DoesNotCallAgentHealthReporter_IfTransactionWasAlreadyFinished()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.Finish()).Returns(false);
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            var transactionMetricName = new TransactionMetricName("c", "d");
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Never());
        }

        [Test]
        public void OnTransactionFinalized_CallsTransform_IfTransactionWasAlreadyFinished()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            var transactionMetricName = new TransactionMetricName("c", "d");
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<IInternalTransaction>()));
        }

        [Test]
        public void OnTransactionFinalized_DoesNotCallTransform_IfTransactionWasAlreadyFinished()
        {
            var startTime = DateTime.Now;
            var segments = new Segment[]
            {
                GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
                GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
                GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
            };
            var transaction = BuildTestTransaction(segments, startTime);
            var mockedTransaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => mockedTransaction.Finish()).Returns(false);
            Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

            var transactionMetricName = new TransactionMetricName("c", "d");
            Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

            EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

            Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<IInternalTransaction>()), Occurs.Never());
        }

        #endregion OnTransactionFinalized

        private ImmutableTransaction BuildTestTransaction(IEnumerable<Segment> segments = null, DateTime? startTime = null)
        {
            var transactionMetadata = new TransactionMetadata("transactionGuid");

            var name = TransactionName.ForWebTransaction("foo", "bar");
            segments = segments ?? Enumerable.Empty<Segment>();
            var metadata = transactionMetadata.ConvertToImmutableMetadata();
            startTime = startTime ?? DateTime.Now;
            var duration = TimeSpan.FromSeconds(1);
            var guid = Guid.NewGuid().ToString();

            return new ImmutableTransaction(name, segments, metadata, startTime.Value, duration, duration, guid, false, false, false, 1.23f, false, string.Empty, null, _attribDefs);
        }

        private static Segment GetUnfinishedSegment(DateTime transactionStartTime, DateTime startTime)
        {
            return GetFinishedSegment(transactionStartTime, startTime, null);
        }

        private static Segment GetFinishedSegment(DateTime transactionStartTime, DateTime startTime, TimeSpan? duration)
        {
            var segment = new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("type", "method", 1));
            segment.SetSegmentData(new SimpleSegmentData(""));

            return new Segment(startTime - transactionStartTime, duration, segment, null);
        }
    }
}
