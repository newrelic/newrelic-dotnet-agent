using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Utils;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace CompositeTests
{
	[TestFixture]
	public class AgentApiTests
	{
		[NotNull]
		private static CompositeTestAgent _compositeTestAgent;

		private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";

		[SetUp]
		public void SetUp()
		{
			_compositeTestAgent = new CompositeTestAgent();
		}

		[TearDown]
		public static void TearDown()
		{
			_compositeTestAgent.Dispose();
		}

		#region RecordCustomEvent

		[Test]
		[Description("Verifies a recorded custom event.")]
		public void Test_RecordCustomEvent()
		{
			// ACT
			AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<String, Object> { { "key1", "val1" }, { "key2", "val2" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var customEvent = _compositeTestAgent.CustomEvents.FirstOrDefault();
			var expectedEventAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "key1", Value = "val1"},
				new ExpectedAttribute {Key = "key2", Value = "val2"}
			};
			CustomEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.UserAttributes, customEvent);
		}

		[Test]
		[Description("Verifies a recorded custom event during high security mode.")]
		public void Test_RecordCustomEvent_DuringHighSecurity()
		{
			// ARRANGE
			_compositeTestAgent.ServerConfiguration.HighSecurityEnabled = true;
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			// ACT
			AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<String, Object> { { "key1", "val1" }, { "key2", "val2" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var customEvents = _compositeTestAgent.CustomEvents;
			Assert.IsEmpty(customEvents);
		}

		[Test]
		[Description("Verifies a recorded custom event with a null-valued attribute.")]
		public void Test_RecordCustomEvent_WithNullValuedAttribute()
		{
			// ACT
			AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<String, Object> { { "key1", "val1" }, { "key2", null }, { "key3", "val3" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var customEvent = _compositeTestAgent.CustomEvents.FirstOrDefault();
			var expectedEventAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "key1", Value = "val1"},
				new ExpectedAttribute {Key = "key3", Value = "val3"}
			};
			CustomEventAssertions.HasAttributes(expectedEventAttributes, AttributeClassification.UserAttributes, customEvent);
		}

		#endregion

		#region RecordMetric

		[Test]
		[Description("Verifies a recorded metric.")]
		public void Test_RecordMetric()
		{
			// ACT
			AgentApi.RecordMetric("MyCustomMetric", 1.4f);
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 1}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		[Test]
		[Description("Verifies a recorded metric after a response time metric.")]
		public void Test_RecordMetric_following_RecordResponseTimeMetric()
		{
			// ACT
			AgentApi.RecordResponseTimeMetric("MyCustomMetric", 1000);
			AgentApi.RecordMetric("MyCustomMetric", 1.4f);
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 2}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		#endregion

		#region RecordResponseTimeMetric

		[Test]
		[Description("Verifies a recorded response time metric.")]
		public void Test_RecordResponseTimeMetric()
		{
			// ACT
			AgentApi.RecordResponseTimeMetric("MyCustomMetric", 1000);
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric"}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		[Test]
		[Description("Verifies a recorded response time metric after a recorded metric.")]
		public void Test_RecordResponseTimeMetric_following_RecordMetric()
		{
			// ACT
			AgentApi.RecordMetric("MyCustomMetric", 1.4f);
			AgentApi.RecordResponseTimeMetric("MyCustomMetric", 1000);
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 2}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		#endregion

		#region IncrementCounter

		[Test]
		[Description("Verifies an incremented counter.")]
		public void Test_IncrementCounter()
		{
			// ACT
			AgentApi.IncrementCounter("MyCustomMetric");
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "MyCustomMetric", CallCount = 1},
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 2}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		[Test]
		[Description("Verifies an incremented counter (with Custom namespace) after recorded time metric.")]
		public void Test_IncrementCounter_AfterExistingTimeMetric()
		{
			// ACT
			AgentApi.RecordMetric("MyCustomMetric", 1.4f);
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				// Note: the legacy agent used to drop any IncrementCounter calls that occurred after a RecordMetric call with the same metric name
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 3}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		[Test]
		[Description("Verifies an incremented counter (with Custom namespace) before recorded time metric.")]
		public void Test_IncrementCounter_BeforeExistingTimeMetric()
		{
			// ACT
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			AgentApi.IncrementCounter("Custom/MyCustomMetric");
			AgentApi.RecordMetric("MyCustomMetric", 1.4f);
			_compositeTestAgent.Harvest();

			// ASSERT
			var customMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedMetric>
			{
				// Note: the legacy agent used to drop any RecordMetric calls that occurred after an IncrementCounter call with the same metric name
				new ExpectedTimeMetric {Name = "Custom/MyCustomMetric", CallCount = 3}
			};
			MetricAssertions.MetricsExist(expectedMetrics, customMetrics);
		}

		#endregion

		#region NoticeError

		[Test]
		[Description("Verifies a reported error with an Exception when in a transaction.")]
		public void Test_NoticeError_WithException()
		{
			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError(new Exception("This is a new exception."));
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, "This is a new exception.");
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");
			Assert.AreEqual(errorTrace.Guid, _compositeTestAgent.TransactionTraces.First().Guid);

			var expectedErrorAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
		}

		[Test]
		[Description("Verifies a reported error with an Exception when in a transaction with stripErrorMessagesEnabled.")]
		public void Test_NoticeError_WithException_StripErrorMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError(new Exception("This is a new exception."));
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");
			Assert.AreEqual(errorTrace.Guid, _compositeTestAgent.TransactionTraces.First().Guid);

			var expectedErrorAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
		}

		[Test]
		[Description("Verifies a reported error with an Exception when in a transaction without error message in high security mode.")]
		public void Test_NoticeError_WithException_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError(new Exception("This is a new exception."));
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");
			Assert.AreEqual(errorTrace.Guid, _compositeTestAgent.TransactionTraces.First().Guid);

			var expectedErrorAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
		}

		[Test]
		[Description("Verifies a reported error with an Exception when outside of a transaction.")]
		public void Test_NoticeErrorOutsideTransaction_WithException()
		{
			// ACT
			AgentApi.NoticeError(new Exception("This is a new exception."));
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, "This is a new exception.");
			Assert.AreEqual(errorTrace.Path, "NewRelic.Api.Agent.NoticeError API Call");
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
		}

		[Test]
		[Description("Verifies a reported error with an Exception when outside of a transaction with StripExceptionMessages enabled.")]
		public void Test_NoticeErrorOutsideTransaction_WithException_StripExceptionMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError(new Exception("This is a new exception."));
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.AreEqual(errorTrace.Path, "NewRelic.Api.Agent.NoticeError API Call");
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
		}


		[Test]
		[Description("Verifies a reported error with an Exception when outside of a transaction without error message in high security mode.")]
		public void Test_NoticeErrorOutsideTransaction_WithException_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError(new Exception("This is a new exception."));
			_compositeTestAgent.Harvest();

			// ASSERT
			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.AreEqual(errorTrace.Path, "NewRelic.Api.Agent.NoticeError API Call");
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters when in a transaction.")]
		public void Test_NoticeError_WithExceptionAndCustomParams()
		{
			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			_compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment").End();
			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String> { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedErrorUserAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, "This is a new exception.");
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters when in a transaction with StripExceptionMessages enabled.")]
		public void Test_NoticeError_WithExceptionAndCustomParams_StripExceptionMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			_compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment").End();
			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String> { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedErrorUserAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters but without an error message when in a transaction in high security.")]
		public void Test_NoticeError_WithExceptionAndCustomParams_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			_compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment").End();
			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String> { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var unexpectedErrorUserAttributes = new List<String>
			{
				"attribute1"
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction.")]
		public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams()
		{
			// ACT
			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, "This is a new exception.");
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction with StripExceptionMessages.")]
		public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_StripExceptionMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction without error message in high security.")]
		public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError(new Exception("This is a new exception."), new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var unexpectedAttributes = new List<string>
			{
				"attribute1"
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "System.Exception");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction.")]
		public void Test_NoticeError_WithMessageAndCustomParams()
		{
			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedUserAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(errorTrace.Message, "This is an exception string.");
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedUserAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction with StripExceptionMessages enabled.")]
		public void Test_NoticeError_WithMessageAndCustomParams_StripExceptionMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedUserAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedUserAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error without an error message or custom parameters when in a transaction in high security.")]
		public void Test_NoticeError_WithMessageAndCustomParams_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var unexpectedAttributes = new List<string>
			{
				"attribute1"
			};

			var unexpectedNonErrorAttributes = new List<String>
			{
				"attribute1"
			};

			var expectedErrorAgentAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();
			var transactionTrace = _compositeTestAgent.TransactionTraces.First();
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.AreEqual(errorTrace.Path, "WebTransaction/ASP/TransactionName");

			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
			ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction when outside of a transaction.")]
		public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams()
		{
			// ACT
			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(errorTrace.Message, "This is an exception string.");
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction when outside of a transaction with StripExceptionMessagesEnabled.")]
		public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_StripExceptionMessages()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "attribute1", Value = "value1"}
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(errorTrace.Message, StripExceptionMessagesMessage);
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies a reported error without an error message or custom parameters when in a transaction when outside of a transaction in high security.")]
		public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_HighSecurity()
		{
			// ACT
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			AgentApi.NoticeError("This is an exception string.", new Dictionary<String, String>() { { "attribute1", "value1" } });
			_compositeTestAgent.Harvest();

			// ASSERT
			var unexpectedAttributes = new List<string>
			{
				"attribute1"
			};

			var errorTrace = _compositeTestAgent.ErrorTraces.First();

			Assert.AreEqual(errorTrace.ExceptionClassName, "Custom Error");
			Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
			Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
			Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
			ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);
		}

		[Test]
		[Description("Verifies the metrics used for calculating error % are recorded.")]
		public void Test_NoticeError_ErrorMetrics()
		{
			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.NoticeError(new Exception("This is the first exception."), new Dictionary<String, String>() { { "attribute1", "value1" } });
			AgentApi.NoticeError(new Exception("This is the second exception."), new Dictionary<String, String>() { { "attribute2", "value2" } });
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric {Name = "Errors/all", CallCount = 1}, // Count Stats
				new ExpectedCountMetric {Name = "Errors/allWeb", CallCount = 1}, // Count Stats
				new ExpectedCountMetric {Name = "Errors/WebTransaction/ASP/TransactionName", CallCount = 1}, // Count Stats
				new ExpectedTimeMetric {Name = "WebTransaction", CallCount = 1}, // Tick Stats
				new ExpectedTimeMetric {Name = "WebTransaction/ASP/TransactionName", CallCount = 1}, // Tick Stats
				new ExpectedApdexMetric {Name = "Apdex", FrustratingCount = 1}, // Apdex Stats
				new ExpectedApdexMetric {Name = "ApdexAll", FrustratingCount = 1}, // Apdex Stats, frustrating count = 1
				new ExpectedApdexMetric {Name = "Apdex/ASP/TransactionName", FrustratingCount = 1}, // Apdex Stats
				new ExpectedCountMetric {Name = "Supportability/ApiInvocation/NoticeError", CallCount = 2}, // Count Stats
				new ExpectedTimeMetric {Name = "HttpDispatcher", CallCount = 1}, // Tick Stats, Max Min SoS...
			};

			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		[Test]
		[Description("Verifies the error rate for transactions is reported correctly (KT error rates).")]
		public void Test_NoticeError_TransactionErrorAttributes()
		{
			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			_compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment").End();
			AgentApi.NoticeError(new Exception("This is a new exception."));
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionEvent = _compositeTestAgent.TransactionEvents.First();
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "errorType", Value = "System.Exception"},
				new ExpectedAttribute {Key = "errorMessage", Value = "This is a new exception."},
				new ExpectedAttribute {Key = "type", Value = "Transaction"},
				new ExpectedAttribute {Key = "name", Value = "WebTransaction/ASP/TransactionName"}
			};
			
			TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.Intrinsics, transactionEvent);
		}

		#endregion

		#region AddCustomParameter
		private const int MaxNumCustomParams = 64;

		[Test]
		[Description("Verifies a custom parameter added to a transaction.")]
		public void Test_AddCustomParameter()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.AddCustomParameter("key1", "val1");
			AgentApi.AddCustomParameter("key2", 2.0d);
			AgentApi.AddCustomParameter("key3", 3.1d);
			AgentApi.AddCustomParameter("key4", 4.0f);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "key1", Value = "val1"},

				// Doubles should be turned into strings
				new ExpectedAttribute {Key = "key2", Value = "2"},
				new ExpectedAttribute {Key = "key3", Value = "3.1"},

				// Singles should be left as singles
				new ExpectedAttribute {Key = "key4", Value = 4.0f}
			};

			TransactionTraceAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies the max number of custom parameters added to a transaction.")]
		public void Test_AddMaxCustomParameters()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			for (var i = 1; i <= MaxNumCustomParams; i++)
			{
				AgentApi.AddCustomParameter(String.Format("key{0}", i), i);
			}
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var expectedAttributes = new List<ExpectedAttribute> { };
			for (var i = 1; i <= MaxNumCustomParams; i++)
			{
				expectedAttributes.Add(new ExpectedAttribute { Key = String.Format("key{0}", i), Value = String.Format("{0}", i) });
			}

			TransactionTraceAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies adding more than the max number of custom parameters to a transaction .")]
		public void Test_AddMoreThanMaxCustomParameters()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			for (var i = 1; i <= MaxNumCustomParams + 1; i++) // note the + 1
			{
				AgentApi.AddCustomParameter(String.Format("key{0}", i), i);
			}
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var expectedAttributes = new List<ExpectedAttribute> { };
			for (var i = 1; i <= MaxNumCustomParams; i++) // the + 1 does not appear here...anything over the max gets swallowed
			{
				expectedAttributes.Add(new ExpectedAttribute { Key = String.Format("key{0}", i), Value = String.Format("{0}", i) });
			}
			var unexpectedAttributes = new List<String>
			{
				String.Format("key{0}", MaxNumCustomParams + 1)
			};

			TransactionTraceAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}


		[Test]
		[Description("Verifies a custom parameter during high security")]
		public void Test_AddCustomParameter_DuringHighSecurity()
		{
			// ARRANGE
			_compositeTestAgent.ServerConfiguration.HighSecurityEnabled = true;
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.AddCustomParameter("key1", "val1");
			AgentApi.AddCustomParameter("key2", "val2");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var unexpectedAttributes = new List<String> { "key1", "key2" };

			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a custom parameter added to a transaction with a null value.")]
		public void Test_AddCustomParameter_WithNullValue()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.AddCustomParameter("key1", null as String);
			AgentApi.AddCustomParameter("key2", null as IConvertible);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var unexpectedAttributes = new List<String>
			{
				"key1",
				"key2"
			};

			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
		}

		[Test]
		[Description("Verifies a custom parameter added to a transaction with a null key.")]
		public void Test_AddCustomParameter_WithNullKey()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.AddCustomParameter(null, "some value");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT -- simply getting data at all is sufficient to prove that nothing broke due to the bogus API call
			Assert.NotNull(_compositeTestAgent.TransactionTraces.FirstOrDefault());
			Assert.NotNull(_compositeTestAgent.TransactionEvents.FirstOrDefault());
		}

		#endregion

		#region SetTransactionName

		[Test]
		[Description("Verifies a custom transaction name creates supportability and web transaction metrics.")]
		public void Test_SetTransactionName()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.SetTransactionName("MyCategory", "MyTransaction");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "WebTransaction/MyCategory/MyTransaction", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that the agent API isn't overridden by other lower priority names or same priority names called afterwards.")]
		public void Test_SetTransactionName_NotOverriddenByLowerPriorityName()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority-1", -1);
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority0", 0);
			transaction.SetWebTransactionName(WebTransactionType.Action, "priority6", 7);
			AgentApi.SetTransactionName("MyCategory", "MyTransaction");
			transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority-1", -1);
			transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority0", 0);
			transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority6", 7);
			transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority8", 8);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "WebTransaction/MyCategory/MyTransaction", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that the agent API overrides other name with same priority that was set first.")]
		public void Test_SetTransactionName_OverridesSamePriorityName_ThatWasSetFirst()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			transaction.SetWebTransactionName(WebTransactionType.Action, "UserPriority", AgentApi.UserTransactionNamePriority);
			AgentApi.SetTransactionName("MyCategory", "MyTransaction");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "WebTransaction/MyCategory/MyTransaction", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that the agent API is overridden by other name with same priority that was set last.")]
		public void Test_SetTransactionName_OverriddenBySamePriorityName_ThatWasSetLast()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.SetTransactionName("MyCategory", "MyTransaction");
			transaction.SetWebTransactionName(WebTransactionType.Action, "UserPriority", AgentApi.UserTransactionNamePriority);
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "WebTransaction/Action/UserPriority", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		[Test]
		[Description("Verifies that, with multiple SetTransactionName calls, the last call takes effect.")]
		public void Test_SetTransactionName_MultipleCalls_LastCallWins()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.SetTransactionName("MyCategory", "MyTransaction");
			AgentApi.SetTransactionName("MyCategory", "MyTransaction2");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "WebTransaction/MyCategory/MyTransaction2", CallCount = 1}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
		}

		#endregion SetTransactionName

		#region SetUserParameters

		[Test]
		[Description("Verifies user parameters set via the 'SetUserParameters' API method.")]
		public void Test_SetUserParameters()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.SetUserParameters("MyUserName", "MyAccountName", "MyAppName");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric { Name = "Supportability/ApiInvocation/SetUserParameters" }
			};
			var expectedAttributes = new List<ExpectedAttribute>
			{
				new ExpectedAttribute {Key = "user", Value = "MyUserName"},
				new ExpectedAttribute {Key = "account", Value = "MyAccountName"},
				new ExpectedAttribute {Key = "product", Value = "MyAppName"}
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
			TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionEvent);
			TransactionTraceAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, transactionTrace);
		}

		[Test]
		[Description("Verifies user parameters set via the 'SetUserParameters' API method.")]
		public void Test_SetUserParameters_WithHighSecurity()
		{
			// ARRANGE
			_compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
			_compositeTestAgent.PushConfiguration();

			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "name");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.SetUserParameters("MyUserName", "MyAccountName", "MyAppName");
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
			var transactionEvent = _compositeTestAgent.TransactionEvents.FirstOrDefault();
			var expectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric {Name = "Supportability/ApiInvocation/SetUserParameters"}
			};
			var unexpectedAttributes = new List<string>
			{
				"user",
				"account",
				"product"
			};

			MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
			TransactionEventAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes,
				transactionEvent);
			TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes,
				transactionTrace);
		}

		#endregion

		#region IgnoreTransaction

		[Test]
		[Description("Ignore Transaction - Metrics")]
		public void Test_IgnoreTransaction()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "TransactionName");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.IgnoreTransaction();
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var unexpectedMetrics = new List<ExpectedTimeMetric>
			{
				new ExpectedTimeMetric { Name = "WebTransaction/Action/TransactionName" }
			};

			MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
			Assert.IsEmpty(_compositeTestAgent.TransactionTraces);
			Assert.IsEmpty(_compositeTestAgent.TransactionEvents);
		}

		#endregion

		#region IgnoreApdex

		[Test]
		public void Test_IgnoreApdex()
		{
			// ARRANGE
			var agentWrapperApi = _compositeTestAgent.GetAgentWrapperApi();

			// ACT
			var transaction = agentWrapperApi.CreateWebTransaction(WebTransactionType.Action, "TransactionName");
			var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
			segment.End();
			AgentApi.IgnoreApdex();
			transaction.End();
			_compositeTestAgent.Harvest();

			// ASSERT
			var actualMetrics = _compositeTestAgent.Metrics;
			var unexpectedMetrics = new List<ExpectedApdexMetric>
			{
				new ExpectedApdexMetric { Name = "Apdex" },
				new ExpectedApdexMetric { Name = "ApdexAll" },
				new ExpectedApdexMetric { Name = "Apdex/Action/TransactionName" }
			};

			MetricAssertions.MetricsDoNotExist(unexpectedMetrics, actualMetrics);
		}

		#endregion

		#region GetBrowserTimingHeader

		[Test]
		public void Test_GetBrowserTimingHeader()
		{
			_compositeTestAgent.ServerConfiguration.RumSettingsJavaScriptAgentLoader = "The Agent";
			_compositeTestAgent.LocalConfiguration.service.licenseKey = "license key";
			_compositeTestAgent.LocalConfiguration.browserMonitoring.autoInstrument = false;
			_compositeTestAgent.PushConfiguration();

			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();
			var browserHeader = AgentApi.GetBrowserTimingHeader();
			transaction.End();

			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric { Name = "Supportability/ApiInvocation/GetBrowserTimingHeader", CallCount = 1 }
			};

			Assert.IsTrue(browserHeader.Contains("NREUM")); // Asserting that the header DOES contains a known RUM identifier
			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		[Test]
		public void ShouldOnlyReturnRumOnFirstCallPerTransaction()
		{
			_compositeTestAgent.ServerConfiguration.RumSettingsJavaScriptAgentLoader = "The Agent";
			_compositeTestAgent.LocalConfiguration.service.licenseKey = "license key";
			_compositeTestAgent.LocalConfiguration.browserMonitoring.autoInstrument = false;
			_compositeTestAgent.PushConfiguration();

			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			segment.End();

			var firstHeader = AgentApi.GetBrowserTimingHeader();
			var secondHeader = AgentApi.GetBrowserTimingHeader();
			var thirdHeader = AgentApi.GetBrowserTimingHeader();

			transaction.End();


			Assert.AreNotEqual(String.Empty, firstHeader);
			Assert.AreEqual(String.Empty, secondHeader);
			Assert.AreEqual(String.Empty, thirdHeader);
		}

		#endregion

		#region DisableBrowserMonitoring

		[Test]
		public void Test_DisableBrowserMonitoring()
		{
			//AgentApi.DisableBrowserMonitoring();
			_compositeTestAgent.ServerConfiguration.RumSettingsJavaScriptAgentLoader = "The Agent";
			_compositeTestAgent.PushConfiguration();

			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			_compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment").End();
			AgentApi.DisableBrowserMonitoring(true);
			var browserHeader = AgentApi.GetBrowserTimingHeader();
			transaction.End();

			_compositeTestAgent.Harvest();

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric { Name = "Supportability/ApiInvocation/DisableBrowserMonitoring", CallCount = 1 }
			};

			Assert.IsEmpty(browserHeader); // Asserting that the header DOES NOT contains a known RUM identifier
			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		#endregion

		#region StartAgent

		[Test]
		public void Test_StartAgent()
		{
			// ACT
			AgentApi.StartAgent();
			_compositeTestAgent.Harvest();
			var agentVersion = AgentVersion.Version;

			// ASSERT
			var expectedMetrics = new List<ExpectedMetric>
			{
				new ExpectedCountMetric { Name = $"Supportability/AgentVersion/{agentVersion}", CallCount = 1 }
			};

			// This test does not verify that the agent is started, only that the expected metric is generated.  Integration
			// tests are able to verify the StartAgent functionality.
			MetricAssertions.MetricsExist(expectedMetrics, _compositeTestAgent.Metrics);
		}

		#endregion

		#region SetApplicationName

		[Test]
		public void Test_SetApplicationName()
		{
			// ACT
			AgentApi.SetApplicationName("MyApplicationName");

			// ASSERT
			_compositeTestAgent.PushConfiguration();
			var appNames = _compositeTestAgent.CurrentConfiguration.ApplicationNames;

			Assert.True(appNames.Contains("MyApplicationName"));
		}

		#endregion

		#region GetRequestMetadata

		[Test]
		public void Test_GetRequestMetadata()
		{
			var NewRelicIdHttpHeader = "X-NewRelic-ID";
			var TransactionDataHttpHeader = "X-NewRelic-Transaction";

			_compositeTestAgent.ServerConfiguration.EncodingKey = "foo";
			_compositeTestAgent.PushConfiguration();

			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			var requestMetadata = AgentApi.GetRequestMetadata().ToDictionary(x => x.Key, x => x.Value);
			segment.End();
			transaction.End();

			_compositeTestAgent.Harvest();

			// ASSERT
			Assert.IsTrue(requestMetadata.Count() == 2);
			NrAssert.Multiple(
				() => Assert.IsTrue(requestMetadata.ContainsKey(NewRelicIdHttpHeader)),
				() => Assert.IsTrue(requestMetadata.ContainsKey(TransactionDataHttpHeader))
				);

			var crossProcessId = Strings.TryBase64Decode(requestMetadata[NewRelicIdHttpHeader], _compositeTestAgent.ServerConfiguration.EncodingKey);
			Assert.AreEqual(_compositeTestAgent.ServerConfiguration.CatId, crossProcessId);

			var crossApplicationRequestData = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationRequestData>(requestMetadata[TransactionDataHttpHeader], _compositeTestAgent.ServerConfiguration.EncodingKey);
			Assert.NotNull(crossApplicationRequestData);

			NrAssert.Multiple(
				() => Assert.NotNull(crossApplicationRequestData.TransactionGuid),
				() => Assert.AreEqual(false, crossApplicationRequestData.Unused),
				() => Assert.NotNull(crossApplicationRequestData.TripId),
				() => Assert.NotNull(crossApplicationRequestData.PathHash)
				);
		}
		#endregion

		#region GetResponseMetadata

		[Test]
		public void Test_GetResponseMetadata()
		{
			var AppDataHttpHeader = "X-NewRelic-App-Data";

			var trustedAccount = Int64.Parse(_compositeTestAgent.ServerConfiguration.CatId.Split(new []{'#'})[0]);
			_compositeTestAgent.ServerConfiguration.TrustedIds = new long[] {trustedAccount};
			_compositeTestAgent.ServerConfiguration.EncodingKey = "foo";
			_compositeTestAgent.PushConfiguration();

			// ACT
			var transaction = _compositeTestAgent.GetAgentWrapperApi().CreateWebTransaction(WebTransactionType.ASP, "TransactionName");
			var segment = _compositeTestAgent.GetAgentWrapperApi().StartTransactionSegmentOrThrow("segment");
			_compositeTestAgent.GetAgentWrapperApi().ProcessInboundRequest(AgentApi.GetRequestMetadata()); // we test this elsewhere
			segment.End();
			var responseMetadata = AgentApi.GetResponseMetadata().ToDictionary(x => x.Key, x => x.Value);
			transaction.End();

			_compositeTestAgent.Harvest();

			// ASSERT
			Assert.IsTrue(responseMetadata.Count() == 1);
			Assert.IsTrue(responseMetadata.ContainsKey(AppDataHttpHeader));

			var crossApplicationResponseData = HeaderEncoder.TryDecodeAndDeserialize<CrossApplicationResponseData>(responseMetadata[AppDataHttpHeader], _compositeTestAgent.ServerConfiguration.EncodingKey);
			Assert.NotNull(crossApplicationResponseData);

			NrAssert.Multiple(
				() => Assert.AreEqual(_compositeTestAgent.ServerConfiguration.CatId, crossApplicationResponseData.CrossProcessId),
				() => Assert.AreEqual("WebTransaction/ASP/TransactionName", crossApplicationResponseData.TransactionName),
				() => Assert.NotNull(crossApplicationResponseData.QueueTimeInSeconds),
				() => Assert.IsTrue(crossApplicationResponseData.ResponseTimeInSeconds > 0),
				() => Assert.NotNull(crossApplicationResponseData.ContentLength),
				() => Assert.NotNull(crossApplicationResponseData.TransactionGuid)
				);
		}
		#endregion

	}
}
