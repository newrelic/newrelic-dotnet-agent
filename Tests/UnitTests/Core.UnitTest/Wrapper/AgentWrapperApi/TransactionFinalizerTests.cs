using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Transactions.TransactionNames;
using NewRelic.Agent.Core.Transformers.TransactionTransformer;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi
{
	[TestFixture]
	public class TransactionFinalizerTests
	{
		[NotNull]
		private TransactionFinalizer _transactionFinalizer;

		[NotNull]
		private IAgentHealthReporter _agentHealthReporter;

		[NotNull]
		private ITransactionMetricNameMaker _transactionMetricNameMaker;

		[NotNull]
		private IPathHashMaker _pathHashMaker;

		[NotNull]
		private ITransactionTransformer _transactionTransformer;

		[SetUp]
		public void SetUp()
		{
			_agentHealthReporter = Mock.Create<IAgentHealthReporter>();
			_transactionMetricNameMaker = Mock.Create<ITransactionMetricNameMaker>();
			_pathHashMaker = Mock.Create<IPathHashMaker>();
			_transactionTransformer = Mock.Create<ITransactionTransformer>();

			_transactionFinalizer = new TransactionFinalizer(_agentHealthReporter, _transactionMetricNameMaker, _pathHashMaker, _transactionTransformer);
		}

		[TearDown]
		public void TearDown()
		{
			_transactionFinalizer.Dispose();
		}

		#region Finish

		[Test]
		public void Finish_UpdatesTransactionPathHash()
		{
			var transaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => transaction.Finish()).Returns(true);

			var transactionName = new WebTransactionName("a", "b");
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
			var transaction = Mock.Create<ITransaction>();

			_transactionFinalizer.Finish(transaction);

			Mock.Assert(() => transaction.Finish());
		}

		#endregion Finish

		#region OnTransactionFinalized

		[Test]
		public void OnTransactionFinalized_CallsForceChangeDurationWith1Millisecond_IfNoSegments()
		{
			var transaction = BuildTestTransaction();
			var internalTransaction = Mock.Create<ITransaction>();
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
				GetUnfinishedSegment(startTime, startTime.AddSeconds(1)),
				GetUnfinishedSegment(startTime, startTime.AddSeconds(2)),
				GetUnfinishedSegment(startTime, startTime.AddSeconds(0))
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
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
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
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
				GetUnfinishedSegment(startTime, startTime.AddSeconds(5)),
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3)),
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

			EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

			Mock.Assert(() => mockedTransaction.ForceChangeDuration(TimeSpan.FromSeconds(5)));
		}

		[Test]
		public void OnTransactionFinalized_UpdatesTransactionPathHash()
		{
			var transaction = BuildTestTransaction();
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

			var transactionName = new WebTransactionName("a", "b");
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
			var mockedTransaction = Mock.Create<ITransaction>();
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
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3))
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);
			
			var transactionMetricName = new TransactionMetricName("c", "d");
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

			EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

			Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, Arg.IsAny<String>(), Arg.IsAny<String>()));
		}

		[Test]
		public void OnTransactionFinalized_DoesNotCallAgentHealthReporter_IfTransactionWasAlreadyFinished()
		{
			var startTime = DateTime.Now;
			var segments = new Segment[]
			{
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3))
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.Finish()).Returns(false);
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

			var transactionMetricName = new TransactionMetricName("c", "d");
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

			EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

			Mock.Assert(() => _agentHealthReporter.ReportTransactionGarbageCollected(transactionMetricName, Arg.IsAny<String>(), Arg.IsAny<String>()), Occurs.Never());
		}

		[Test]
		public void OnTransactionFinalized_CallsTransform_IfTransactionWasAlreadyFinished()
		{
			var startTime = DateTime.Now;
			var segments = new Segment[]
			{
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3))
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.Finish()).Returns(true);
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

			var transactionMetricName = new TransactionMetricName("c", "d");
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

			EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

			Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<ITransaction>()));
		}

		[Test]
		public void OnTransactionFinalized_DoesNotCallTransform_IfTransactionWasAlreadyFinished()
		{
			var startTime = DateTime.Now;
			var segments = new Segment[]
			{
				GetFinishedSegment(startTime, startTime.AddSeconds(1), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(2), TimeSpan.FromSeconds(1)),
				GetFinishedSegment(startTime, startTime.AddSeconds(0), TimeSpan.FromSeconds(3))
			};
			var transaction = BuildTestTransaction(segments, startTime);
			var mockedTransaction = Mock.Create<ITransaction>();
			Mock.Arrange(() => mockedTransaction.Finish()).Returns(false);
			Mock.Arrange(() => mockedTransaction.ConvertToImmutableTransaction()).Returns(transaction);

			var transactionMetricName = new TransactionMetricName("c", "d");
			Mock.Arrange(() => _transactionMetricNameMaker.GetTransactionMetricName(Arg.IsAny<ITransactionName>())).Returns(transactionMetricName);

			EventBus<TransactionFinalizedEvent>.Publish(new TransactionFinalizedEvent(mockedTransaction));

			Mock.Assert(() => _transactionTransformer.Transform(Arg.IsAny<ITransaction>()), Occurs.Never());
		}

		#endregion OnTransactionFinalized

		private static ImmutableTransaction BuildTestTransaction(IEnumerable<Segment> segments = null, DateTime? startTime = null)
		{
			var transactionMetadata = new TransactionMetadata();

			var name = new WebTransactionName("foo", "bar");
			segments = segments ?? Enumerable.Empty<Segment>();
			var metadata = transactionMetadata.ConvertToImmutableMetadata();
			startTime = startTime ?? DateTime.Now;
			var duration = TimeSpan.FromSeconds(1);
			var guid = Guid.NewGuid().ToString();

			return new ImmutableTransaction(name, segments, metadata, startTime.Value, duration, guid, false, false, false, SqlObfuscator.GetObfuscatingSqlObfuscator());
		}

		private static TypedSegment<SimpleSegmentData> GetUnfinishedSegment(DateTime transactionStartTime, DateTime startTime)
		{
			return GetFinishedSegment(transactionStartTime, startTime, null);
		}

		private static TypedSegment<SimpleSegmentData> GetFinishedSegment(DateTime transactionStartTime, DateTime startTime, TimeSpan? duration)
		{
			return new TypedSegment<SimpleSegmentData>(startTime - transactionStartTime, duration,
				new TypedSegment<SimpleSegmentData>(Mock.Create<ITransactionSegmentState>(), new MethodCallData("type", "method", 1), new SimpleSegmentData(""), false));
		}
	}
}
