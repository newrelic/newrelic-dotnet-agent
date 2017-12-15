using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Collections;
using NewRelic.SystemExtensions;
using NUnit.Framework;

namespace CompositeTests
{
	internal class ApdexTests
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
		public void apdexPerfZone_satisfying_if_time_is_less_than_apdexT()
		{
			// ARRANGE
			var apdexT = TimeSpan.FromMilliseconds(100);
			_compositeTestAgent.ServerConfiguration.ApdexT = apdexT.TotalSeconds;
			_compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
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
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				Thread.Sleep(apdexT.Multiply(2));
				segment.End();
			}
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
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				Thread.Sleep(apdexT.Multiply(5));
				segment.End();
			}
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
			_compositeTestAgent.ServerConfiguration.WebTransactionsApdex = new ConcurrentDictionary<string, double> {{ "WebTransaction/Action/name", keyTransactionApdexT.TotalSeconds}};

			// push the config
			_compositeTestAgent.PushConfiguration();

			// ==== ACT ====
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				Thread.Sleep(keyTransactionApdexT.Multiply(5));
				segment.End();
			}
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
