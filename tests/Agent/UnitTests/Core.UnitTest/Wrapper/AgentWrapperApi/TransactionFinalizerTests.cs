// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.ContinuousProfiling;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi;

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

    #region Continuous profiling span retirement

    [Test]
    public void Finish_RetiresEveryMaterializedSegmentSpanId()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        IReadOnlyList<string> retired = null;
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()))
            .DoInstead((IReadOnlyList<string> ids) => retired = ids);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            var first = GetBaseSegment();
            var second = GetBaseSegment();
            first.SpanId = "1111111111111111";
            second.SpanId = "2222222222222222";
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment> { first, second });
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(Array.Empty<string>());
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Assert.That(retired, Is.Not.Null);
            Assert.That(retired, Is.EquivalentTo(new[] { "1111111111111111", "2222222222222222" }));
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // A segment whose span id was never generated was never pushed to native, so retiring it would be
    // pointless -- and reading SpanId to find out would MINT one, which is the thing to avoid.
    [Test]
    public void Finish_DoesNotRetireOrGenerateSpanIdsForSegmentsThatNeverMaterializedOne()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            var untouched = GetBaseSegment();
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment> { untouched });
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(Array.Empty<string>());
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Mock.Assert(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()), Occurs.Never());
            Assert.That(untouched.TryGetMaterializedSpanId(), Is.Null);
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // Segments past the max-segments cap are nulled out of Segments before the transaction terminates.
    // They still pushed, so they must still be retired -- otherwise a segment-heavy transaction leaves
    // permanent stale links.
    [Test]
    public void Finish_RetiresSpanIdsOfSegmentsDroppedForExceedingTheMaxSegmentsCap()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        IReadOnlyList<string> retired = null;
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()))
            .DoInstead((IReadOnlyList<string> ids) => retired = ids);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            var kept = GetBaseSegment();
            kept.SpanId = "1111111111111111";
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment> { kept });
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(new[] { "3333333333333333" });
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Assert.That(retired, Is.EquivalentTo(new[] { "1111111111111111", "3333333333333333" }));
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // A transaction whose live segments never materialized an id must still retire the dropped ones.
    [Test]
    public void Finish_RetiresDroppedSpanIdsEvenWhenNoLiveSegmentMaterializedOne()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        IReadOnlyList<string> retired = null;
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()))
            .DoInstead((IReadOnlyList<string> ids) => retired = ids);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment>());
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(new[] { "3333333333333333" });
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Assert.That(retired, Is.EquivalentTo(new[] { "3333333333333333" }));
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // Once a transaction exceeds the cap its Segments list carries nulls; enumerating must tolerate them.
    [Test]
    public void Finish_ToleratesNullEntriesInTheSegmentsList()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        IReadOnlyList<string> retired = null;
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()))
            .DoInstead((IReadOnlyList<string> ids) => retired = ids);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            var kept = GetBaseSegment();
            kept.SpanId = "1111111111111111";
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment> { null, kept, null });
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(Array.Empty<string>());
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            Assert.DoesNotThrow(() => _transactionFinalizer.Finish(transaction));
            Assert.That(retired, Is.EquivalentTo(new[] { "1111111111111111" }));
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // The second caller loses the once-only gate in Transaction.Finish, so it must not retire again.
    [Test]
    public void Finish_DoesNotRetireWhenTheTransactionWasAlreadyFinished()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Finish()).Returns(false);

            var result = _transactionFinalizer.Finish(transaction);

            Assert.That(result, Is.False);
            Mock.Assert(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()), Occurs.Never());
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // Every non-CP customer must pay only the static bool read: no Segments enumeration, no allocation.
    [Test]
    public void Finish_DoesNotTouchSegmentsWhenContinuousProfilingIsDisabled()
    {
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        ContinuousProfilingContext.AnyEnabled = false;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Mock.Assert(() => transaction.Segments, Occurs.Never());
            Mock.Assert(() => transaction.DroppedSegmentSpanIds, Occurs.Never());
        }
        finally
        {
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // AnyEnabled is armed before Instance goes live and is deliberately never the final word, so the
    // context's own IsEnabled must still gate the walk -- otherwise a transaction terminating in that
    // window enumerates Segments and allocates for a retirement native would drop anyway.
    [Test]
    public void Finish_DoesNotTouchSegmentsWhenTheContinuousProfilingContextIsNotLive()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(false);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            _transactionFinalizer.Finish(transaction);

            Mock.Assert(() => transaction.Segments, Occurs.Never());
            Mock.Assert(() => transaction.DroppedSegmentSpanIds, Occurs.Never());
            Mock.Assert(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>()), Occurs.Never());
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // A CP failure must never break transaction finalization -- this runs on the GC finalizer thread in
    // the reaped path, where an escaping exception is swallowed by the destructor and the transaction is
    // silently lost.
    [Test]
    public void Finish_StillSucceedsWhenSpanRetirementThrows()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        Mock.Arrange(() => cpContext.RetireSpans(Arg.IsAny<IReadOnlyList<string>>())).Throws<InvalidOperationException>();
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            var kept = GetBaseSegment();
            kept.SpanId = "1111111111111111";
            Mock.Arrange(() => transaction.Segments).Returns(new List<Segment> { kept });
            Mock.Arrange(() => transaction.DroppedSegmentSpanIds).Returns(Array.Empty<string>());
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            bool result = false;
            Assert.DoesNotThrow(() => result = _transactionFinalizer.Finish(transaction));
            Assert.That(result, Is.True);
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    // Reading Segments itself can throw once a transaction is being torn down; that must not escape either.
    [Test]
    public void Finish_StillSucceedsWhenReadingTheSegmentsListThrows()
    {
        var originalInstance = ContinuousProfilingContext.Instance;
        var originalAnyEnabled = ContinuousProfilingContext.AnyEnabled;
        var cpContext = Mock.Create<IContinuousProfilingContext>();
        Mock.Arrange(() => cpContext.IsEnabled).Returns(true);
        ContinuousProfilingContext.Instance = cpContext;
        ContinuousProfilingContext.AnyEnabled = true;
        try
        {
            var transaction = Mock.Create<IInternalTransaction>();
            Mock.Arrange(() => transaction.Segments).Throws<InvalidOperationException>();
            Mock.Arrange(() => transaction.Finish()).Returns(true);

            bool result = false;
            Assert.DoesNotThrow(() => result = _transactionFinalizer.Finish(transaction));
            Assert.That(result, Is.True);
        }
        finally
        {
            ContinuousProfilingContext.Instance = originalInstance;
            ContinuousProfilingContext.AnyEnabled = originalAnyEnabled;
        }
    }

    private static Segment GetBaseSegment()
    {
        return new Segment(TransactionSegmentStateHelpers.GetItransactionSegmentState(), new MethodCallData("Type", "Method", 1));
    }

    #endregion Continuous profiling span retirement

    #region OnTransactionFinalized

    [Test]
    public void OnTransactionFinalized_CallsForceChangeDurationWith1Millisecond_IfNoSegments()
    {
        var transaction = BuildTestTransaction();
        var internalTransaction = Mock.Create<IInternalTransaction>();
        Mock.Arrange(() => internalTransaction.ConvertToImmutableTransaction()).Returns(transaction);

        EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(internalTransaction));

        var expectedTimeSpan = TimeSpan.FromMilliseconds(1);
        Mock.Assert(() => internalTransaction.ForceChangeDuration(expectedTimeSpan));
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
        Mock.Arrange(() => mockedTransaction.Guid).Returns("TestGuid");

        var transactionMetricName = new TransactionMetricName("c", "d");
        Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

        EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

        Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(mockedTransaction.Guid,transactionMetricName, Arg.IsAny<string>(), Arg.IsAny<string>()));
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
        Mock.Arrange(() => mockedTransaction.Guid).Returns("TestGuid");

        var transactionMetricName = new TransactionMetricName("c", "d");
        Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

        EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

        Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(mockedTransaction.Guid, transactionMetricName, Arg.IsAny<string>(), Arg.IsAny<string>()), Occurs.Never());
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