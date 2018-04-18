using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class TransactionNameTests
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

		#region Segment metrics and trace names

		[Test]
		public void SetWebTransactionName_UpdatesTransactionNameCorrectly()
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				transaction.SetWebTransactionName(WebTransactionType.ASP, "foo", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "WebTransaction/ASP/foo"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("WebTransaction/ASP/foo", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetWebTransactionNameFromPath_UpdatesTransactionNameCorrectly()
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				transaction.SetWebTransactionNameFromPath(WebTransactionType.ASP, "foo");
				_agentWrapperApi.StartTransactionSegmentOrThrow("simpleName").End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "WebTransaction/Uri/foo"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("WebTransaction/Uri/foo", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetOtherTransactionName_UpdatesTransactionNameCorrectly()
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				transaction.SetOtherTransactionName("cat", "foo", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "OtherTransaction/cat/foo"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("OtherTransaction/cat/foo", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetMessageBrokerTransactionName_UpdatesTransactionNameCorrectly()
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				transaction.SetMessageBrokerTransactionName(MessageBrokerDestinationType.Queue, "vendor", "dest", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "OtherTransaction/Message/vendor/Queue/Named/dest"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("OtherTransaction/Message/vendor/Queue/Named/dest", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfWebTransaction()
		{
			using (var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				transaction.SetCustomTransactionName("foo", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "WebTransaction/Custom/foo" }
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("WebTransaction/Custom/foo", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfNonWebTransaction()
		{
			using (var transaction = _agentWrapperApi.CreateOtherTransaction("cat", "name"))
			{
				transaction.SetCustomTransactionName("foo", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}
			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				new ExpectedMetric {Name = "OtherTransaction/Custom/foo"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("OtherTransaction/Custom/foo", transactionTrace.TransactionMetricName)
				);
		}

		[Test]
		public void SetCustomTransactionName_UpdatesTransactionNameCorrectly_IfNameIsPrefixedWithCustom()
		{
			using (var transaction = _agentWrapperApi.CreateOtherTransaction("cat", "name"))
			{
				transaction.SetCustomTransactionName("Custom/foo", 4);
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("simpleName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var expectedMetrics = new[]
			{
				// The agent should de-duplicate the "Custom/" prefix that was passed in
				new ExpectedMetric {Name = "OtherTransaction/Custom/foo"}
			};
			var actualMetrics = _compositeTestAgent.Metrics.ToList();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => MetricAssertions.MetricsExist(expectedMetrics, actualMetrics),
				() => Assert.AreEqual("OtherTransaction/Custom/foo", transactionTrace.TransactionMetricName)
				);
		}

		#endregion

		#region naming priorites

		[Test]
		[Description("Verifies that web transaction names are overridden by a higher priority name")]
		public void SetWebTransactionName_OverriddenByHigherPriorityName()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority0", 0);
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority1", 1);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new []
			{
				new ExpectedTimeMetric {Name = "WebTransaction/Action/priority1", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that web transaction names are not overridden by a name with the same priority")]
		public void SetWebTransactionName_NotOverriddenBySamePriorityName()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority1", 1);
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority1again", 1);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new[]
			{
				new ExpectedTimeMetric {Name = "WebTransaction/Action/priority1", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that web transaction names are not overridden by a lower priority name")]
		public void SetWebTransactionName_NotOverriddenByLowerPriorityName()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority1", 1);
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority0", 0);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new[]
			{
				new ExpectedTimeMetric {Name = "WebTransaction/Action/priority1", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}


		#endregion
	}
}
