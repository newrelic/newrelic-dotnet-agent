using System;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class TransactionTests
	{
		[NotNull]
		private static CompositeTestAgent _compositeTestAgent;

		[NotNull]
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
		public void SimpleTransaction_CreatesTransactionTraceAndEvent()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			NrAssert.Multiple(
				() => Assert.AreEqual("WebTransaction/Action/name", transactionTrace.TransactionMetricName),

				() => Assert.AreEqual("WebTransaction/Action/name", transactionEvent.IntrinsicAttributes["name"]),
				() => Assert.AreEqual("Transaction", transactionEvent.IntrinsicAttributes["type"])
				);
		}

		[Test]
		public void FastTransaction_DoesNotCreateTransactionTrace()
		{
			_compositeTestAgent.ServerConfiguration.RpmConfig.TransactionTracerThreshold = TimeSpan.FromSeconds(5);
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				_agentWrapperApi.StartTransactionSegmentOrThrow("segmentName").End();
			}

			_compositeTestAgent.Harvest();

			NrAssert.Multiple(
				() => Assert.AreEqual(0, _compositeTestAgent.TransactionTraces.Count),
				() => Assert.AreEqual(1, _compositeTestAgent.TransactionEvents.Count)
				);
		}

		[Test]
		public void TransactionWithUnfinishedSegments_CreatesTraceAndEvent()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				_agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");

				// Finish the transaction without ending its unfinished segment
			}

			_compositeTestAgent.Harvest();

			NrAssert.Multiple(
				() => Assert.IsTrue(_compositeTestAgent.TransactionTraces.Any()),
				() => Assert.IsTrue(_compositeTestAgent.TransactionEvents.Any())
				);
		}

		[Test]
		public void TransactionWithNoSegments_DoesNotCreateTraceOrEvent()
		{
			_agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name").End();

			_compositeTestAgent.Harvest();

			NrAssert.Multiple(
				() => Assert.IsFalse(_compositeTestAgent.TransactionTraces.Any()),
				() => Assert.IsFalse(_compositeTestAgent.TransactionEvents.Any())
				);
		}

		[Test]
		public void ErrorTransaction_CreatesErrorTraceAndEvent()
		{
			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "rootSegmentMetricName");
			var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");

			transaction.NoticeError(new Exception("Oh no!"));
			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var errorTrace = _compositeTestAgent.ErrorTraces.FirstOrDefault();
			NrAssert.Multiple(
				() => Assert.AreEqual("System.Exception", transactionEvent.IntrinsicAttributes["errorType"]),
				() => Assert.AreEqual("Oh no!", transactionEvent.IntrinsicAttributes["errorMessage"]),
				() => Assert.AreEqual("WebTransaction/Action/rootSegmentMetricName", errorTrace.Path),
				() => Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName),
				() => Assert.AreEqual("Oh no!", errorTrace.Message)
				);
		}
	}
}
