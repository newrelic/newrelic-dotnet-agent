using System;
using System.Linq;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Events;
using NewRelic.Agent.Core.Timing;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Core.CallStack;
using NewRelic.Agent.Core.Database;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	[TestFixture]
	public class TransactionTests
	{

		private IConfiguration _configuration;
		private Transaction _transaction;

		private TransactionFinalizedEvent _publishedEvent;
		private EventSubscription<TransactionFinalizedEvent> _eventSubscription;
		
		private const float Priority = 0.5f;

		[SetUp]
		public void SetUp()
		{
			_configuration = Mock.Create<IConfiguration>();
			var configurationService = Mock.Create<IConfigurationService>();
			Mock.Arrange(() => configurationService.Configuration).Returns(_configuration);

			
			_transaction = new Transaction(_configuration, Mock.Create<ITransactionName>(), Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), Priority, Mock.Create<IDatabaseStatementParser>());
			_publishedEvent = null;
			_eventSubscription = new EventSubscription<TransactionFinalizedEvent>(e => _publishedEvent = e);
		}

		[TearDown]
		public void TearDown()
		{
			_transaction = null;

			_eventSubscription?.Dispose();
			_eventSubscription = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();
		}

		[Test]
		public void TransactionFinalizedEvent_IsPublished_IfNotEndedCleanly()
		{
			Assert.NotNull(_transaction);

			_transaction = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();

			Assert.NotNull(_publishedEvent);
		}

		[Test]
		public void TransactionFinalizedEvent_IsNotPublished_IfEndedCleanly()
		{
			Assert.NotNull(_transaction);

			_transaction.Finish();
			_transaction = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();

			Assert.Null(_publishedEvent);
		}

		[Test]
		public void TransactionShouldOnlyFinishOnce()
		{
			Assert.NotNull(_transaction);
			Assert.False(_transaction.IsFinished, "The transaction should not be finished yet.");

			var finishedTransaction = _transaction.Finish();

			Assert.True(finishedTransaction, "Transaction was not finished when it should have been finished.");
			Assert.True(_transaction.IsFinished, "transaction.IsFinished should be true.");
			Assert.Null(_publishedEvent, "The TransactionFinalizedEvent should not be triggered after the first call to finish.");

			finishedTransaction = _transaction.Finish();
			var isFinished = _transaction.IsFinished;
			_transaction = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();

			Assert.False(finishedTransaction, "Transaction was finished again when it should only be finished once.");
			Assert.True(isFinished, "transaction.IsFinished should still be true.");
			Assert.Null(_publishedEvent, "The TransactionFinalizedEvent should not be triggered when the transaction is already finished.");
		}

		[Test]
		public void TransactionFinalizedEvent_IsNotPublishedASecondTime_IfBuilderGoesOutOfScopeAgain()
		{
			Assert.NotNull(_transaction);

			_transaction = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();

			Assert.NotNull(_publishedEvent);

			// The builder is now pinned to the event, but we can unpin it by unpinning the event
			_publishedEvent = null;

			GC.Collect();
			GC.WaitForFullGCComplete();
			GC.WaitForPendingFinalizers();

			Assert.Null(_publishedEvent);
		}

		[Test]
		[TestCase(1, 2, ExpectedResult = 1)]
		[TestCase(2, 2, ExpectedResult = 2)]
		[TestCase(3, 2, ExpectedResult = 2)]
		public int Add_Segment_When_Segment_Count_Considers_Configuration_TransactionTracerMaxSegments(int transactionTracerMaxSegmentThreashold, int segmentCount)
		{

			Mock.Arrange(() => _configuration.TransactionTracerMaxSegments).Returns(transactionTracerMaxSegmentThreashold);

			var transactionName = TransactionName.ForWebTransaction("WebTransaction", "Test");


			var transaction = new Transaction(_configuration, transactionName, Mock.Create<ITimer>(), DateTime.UtcNow, Mock.Create<ICallStackManager>(), SqlObfuscator.GetObfuscatingSqlObfuscator(), Priority, Mock.Create<IDatabaseStatementParser>());

			for (int i = 0; i < segmentCount; i++)
			{
				new TypedSegment<ExternalSegmentData>(transaction, new MethodCallData("foo" + i, "bar" + i, 1), new ExternalSegmentData(new Uri("http://www.test.com"), "method")).End();
			}
			
			var immutableTransaction = transaction.ConvertToImmutableTransaction();
			return immutableTransaction.Segments.Count();

		}

		[Test]
		public void ConstructedTransactionGuidShouldEqualDistributedTraceTraceId()
		{
			// Arrange
			var name = TransactionName.ForWebTransaction("foo", "bar");
			var startTime = DateTime.Now;
			var timer = Mock.Create<ITimer>();
			var callStackManager = Mock.Create<ICallStackManager>();
			var sqlObfuscator = SqlObfuscator.GetObfuscatingSqlObfuscator();
			var tx = new Transaction(_configuration, name, timer, startTime, callStackManager, sqlObfuscator, Priority, Mock.Create<IDatabaseStatementParser>());

			// Assert
			Assert.That(tx.TransactionMetadata.DistributedTraceTraceId, Is.Not.Null);
			Assert.That(tx.TransactionMetadata.DistributedTraceTraceId, Is.EqualTo(tx.Guid));
		}

		/// <summary>
		/// https://source.datanerd.us/agents/agent-specs/blob/2ad6637ded7ec3784de40fbc88990e06525127b8/Cross-Application-Tracing-PORTED.md#guid
		/// </summary>
		[Test]
		public void TransactionGuidShouldBe16CharacterHex()
		{
			// Arrange
			var name = TransactionName.ForWebTransaction("foo", "bar");
			var startTime = DateTime.Now;
			var timer = Mock.Create<ITimer>();
			var callStackManager = Mock.Create<ICallStackManager>();
			var sqlObfuscator = SqlObfuscator.GetObfuscatingSqlObfuscator();
			var tx = new Transaction(_configuration, name, timer, startTime, callStackManager, sqlObfuscator, Priority, Mock.Create<IDatabaseStatementParser>());

			// Assert
			Assert.That(tx.Guid, Is.Not.Null);

			const string guidFormatPattern = @"^[0-9A-Fa-f]{16}$";
			Assert.That(tx.Guid, Does.Match(guidFormatPattern));
		}

		[Test]
		public void TransactionShouldOnlyCaptureResponseTimeOnce()
		{
			//Verify initial state
			Assert.Null(_transaction.ResponseTime, "ResponseTime should initially be null.");

			//First attempt to capture the response time
			Assert.True(_transaction.TryCaptureResponseTime(), "ResponseTime should have been captured but was not captured.");

			//Verify that the response time was captured
			var capturedResponseTime = _transaction.ResponseTime;
			Assert.NotNull(capturedResponseTime, "ResponseTime should have a value.");

			//Second attempt to capture the response time
			Assert.False(_transaction.TryCaptureResponseTime(), "ResponseTime should not be captured again, but it was.");
			Assert.AreEqual(capturedResponseTime, _transaction.ResponseTime, "ResponseTime should still have the same value as the originally captured ResponseTime.");
		}
	}
}
