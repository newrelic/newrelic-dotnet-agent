using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Transactions;
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

		#region Transaction Events Tests
		[Test]
		public void UnknownRequestUriInTransactionEvent()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.First();
			Assert.AreEqual("/Unknown", transactionEvent.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"]);
		}

		[Test]
		public void RequestUriInTransactionEvent()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				tx.SetUri("myuri");
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.First();
			Assert.AreEqual("myuri", transactionEvent.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"]);
		}

		[Test]
		public void NoRequestUriInTransactionEvent()
		{
			_compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionEvent = _compositeTestAgent.TransactionEvents.First();
			Assert.IsFalse(transactionEvent.GetAttributes(AttributeClassification.AgentAttributes).ContainsKey("request.uri"));
		}
		#endregion

		#region Transaction Traces Tests
		[Test]
		public void UnknownRequestUriInTransactionTrace()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => Assert.AreEqual("/Unknown", transactionTrace.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"]),
				() => Assert.AreEqual("/Unknown", transactionTrace.Uri)
			);
		}

		[Test]
		public void RequestUriInTransactionTrace()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				tx.SetUri("myuri");
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => Assert.AreEqual("myuri", transactionTrace.GetAttributes(AttributeClassification.AgentAttributes)["request.uri"]),
				() => Assert.AreEqual("myuri", transactionTrace.Uri)
			);
		}

		[Test]
		public void NoRequestUriInTransactionTrace()
		{
			_compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				tx.SetUri("myuri");
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			NrAssert.Multiple(
				() => Assert.IsFalse(transactionTrace.GetAttributes(AttributeClassification.AgentAttributes).ContainsKey("request.uri")),
				() => Assert.AreEqual(null, transactionTrace.Uri)
			);
		}
		#endregion


		#region Error Events Test
		// We always exclude the request.uri attribute when there is no transaction, regardless of the attribute inclusion/exclusion logic
		[Test]
		public void NoRequestUriAttributeInErrorEventWithoutTransaction()
		{
			AgentApi.NoticeError(new Exception("oh no"));

			_compositeTestAgent.Harvest();

			var errorEvent = _compositeTestAgent.ErrorEvents.First();
			Assert.IsFalse(errorEvent.AgentAttributes.ContainsKey("request.uri"));
		}

		[Test]
		public void UnknownRequestUriInErrorEventWithTransaction()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				tx.NoticeError(new Exception("test exception"));
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var errorEvent = _compositeTestAgent.ErrorEvents.First();
			Assert.AreEqual("/Unknown", errorEvent.AgentAttributes["request.uri"]);
		}

		[Test]
		public void RequestUriInErrorEventWithTransaction()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				tx.SetUri("myuri");
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				tx.NoticeError(new Exception("test exception"));
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var errorEvent = _compositeTestAgent.ErrorEvents.First();
			Assert.AreEqual("myuri", errorEvent.AgentAttributes["request.uri"]);
		}

		[Test]
		public void NoRequestUriInErrorEventWithTransaction()
		{
			_compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				tx.NoticeError(new Exception("test exception"));
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var errorEvent = _compositeTestAgent.ErrorEvents.First();
			Assert.IsFalse(errorEvent.AgentAttributes.ContainsKey("request.uri"));
		}
		#endregion

		#region Error Traces Tests
		[Test]
		public void RequestUriInErrorTrace()
		{
			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				tx.SetUri("myuri");
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				tx.NoticeError(new Exception("test exception"));
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			Assert.IsTrue(errorTrace.Attributes.AgentAttributes.Any(kv => kv.Key == "request.uri" && (string)kv.Value == "myuri"));
		}

		[Test]
		public void NoRequestUriInErrorTrace()
		{
			_compositeTestAgent.LocalConfiguration.attributes.exclude = new List<string> { "request.uri" };
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name"))
			{
				var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
				tx.NoticeError(new Exception("test exception"));
				segment.End();
			}

			_compositeTestAgent.Harvest();

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			Assert.IsFalse(errorTrace.Attributes.AgentAttributes.Any(kv => kv.Key == "request.uri"));
		}

		[Test]
		public void TransactionEventAndErrorEventUseCorrectTimestampAttributes()
		{
			//Tests Fix for DOTNET-3351 - Transaction timestamp shifted when Error is reported

			_compositeTestAgent.LocalConfiguration.distributedTracing.enabled = true;
			_compositeTestAgent.LocalConfiguration.spanEvents.enabled = true;
			_compositeTestAgent.ServerConfiguration.TrustedAccountKey = "33";
			_compositeTestAgent.PushConfiguration();

			using (var tx = _agentWrapperApi.CreateOtherTransaction("TestCategory", "TestTransaction"))
			{
				var segment = _agentWrapperApi.StartCustomSegmentOrThrow("TestSegment");

				//The sleep ensures that the exception occurs after the transaction
				//start date.  Without this, there is a small chance that the timesetamps 
				//could be the same which would make it difficult to distinguish. 
				Thread.Sleep(5);

				tx.NoticeError(new Exception("This is a test exception"));

				segment.End();
			}

			_compositeTestAgent.Harvest();

			var txEvents = _compositeTestAgent.TransactionEvents;
			var exEvents = _compositeTestAgent.ErrorEvents;

			Assert.AreEqual(1, txEvents.Count);
			Assert.AreEqual(1, exEvents.Count);

			var txEvent = txEvents.First();
			var exEvent = exEvents.First();

			Assert.IsTrue(txEvent.IntrinsicAttributes.ContainsKey("timestamp"));
			Assert.IsTrue(exEvent.IntrinsicAttributes.ContainsKey("timestamp"));

			var txEventTimeStamp = (long)txEvent.IntrinsicAttributes["timestamp"];
			var exEventTimeStamp = (long)exEvent.IntrinsicAttributes["timestamp"];

			Assert.Less(txEventTimeStamp, exEventTimeStamp);
		}
		#endregion

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

		[Test]
		public void AgentTiming_WhenDisabledThenNoAgentTimingMetrics()
		{
			_compositeTestAgent.LocalConfiguration.diagnostics.captureAgentTiming = false;
			_compositeTestAgent.PushConfiguration();
			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "rootSegmentMetricName");
			var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			transaction.End();
			_compositeTestAgent.Harvest();
			var metrics = _compositeTestAgent.Metrics.Where(x => x.MetricName.Name.Contains("AgentTiming"));
			Assert.IsEmpty(metrics);
		}

		[Test]
		public void AgentTiming_WhenEnabledThenAgentTimingMetrics()
		{
			_compositeTestAgent.LocalConfiguration.diagnostics.captureAgentTiming = true;
			_compositeTestAgent.PushConfiguration();
			var transaction = _agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "rootSegmentMetricName");
			var segment = _agentWrapperApi.StartTransactionSegmentOrThrow("segmentName");
			segment.End();
			transaction.End();
			_compositeTestAgent.Harvest();
			var metrics = _compositeTestAgent.Metrics.Where(x => x.MetricName.Name.Contains("AgentTiming"));
			Assert.IsNotEmpty(metrics);
		}
	}
}
