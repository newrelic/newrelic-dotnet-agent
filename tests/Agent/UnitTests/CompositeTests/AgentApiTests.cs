// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core;
using NewRelic.Agent.Core.Api;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Primitives;

namespace CompositeTests
{
    [TestFixture]
    public class AgentApiTests
    {
        private static CompositeTestAgent _compositeTestAgent;

        private const string StripExceptionMessagesMessage = "Message removed by New Relic based on your currently enabled security settings.";
        private const string NoticeErrorPathOutsideTransaction = "NewRelic.Api.Agent.NoticeError API Call";
        private const string ExceptionMessage = "This is a new exception.";
        private IApiSupportabilityMetricCounters _apiSupportabilityMetricCounters;
        private IConfigurationService _configSvc;

        private static readonly string _accountId = "acctid";
        private static readonly string _appId = "appid";
        private static readonly string _guid = "guid";
        private static readonly float _priority = .3f;
        private static readonly bool _sampled = false;
        private static readonly string _traceId = "traceid";
        private static readonly string _trustKey = "trustedkey";
        private static readonly string _type = "typeapp";
        private static readonly string _transactionId = "transactionId";
        private static readonly DistributedTracePayload _distributedTracePayload = DistributedTracePayload.TryBuildOutgoingPayload(_type, _accountId, _appId, _guid, _traceId, _trustKey, _priority, _sampled, DateTime.UtcNow, _transactionId);

        [SetUp]
        public void SetUp()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _compositeTestAgent.ServerConfiguration.AccountId = _accountId;
            _compositeTestAgent.ServerConfiguration.TrustedAccountKey = _trustKey;
            _compositeTestAgent.ServerConfiguration.PrimaryApplicationId = _appId;
            _apiSupportabilityMetricCounters = _compositeTestAgent.Container.Resolve<IApiSupportabilityMetricCounters>();
            _configSvc = _compositeTestAgent.Container.Resolve<IConfigurationService>();
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
            AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<string, object> { { "key1", "val1" }, { "key2", "val2" } });
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
            AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<string, object> { { "key1", "val1" }, { "key2", "val2" } });
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
            AgentApi.RecordCustomEvent("MyCustomEvent", new Dictionary<string, object> { { "key1", "val1" }, { "key2", null }, { "key3", "val3" } });
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
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage));
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);
            Assert.AreEqual(_compositeTestAgent.TransactionTraces.First().Guid, errorTrace.Guid);

            var expectedErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
            };

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = ExceptionMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception when in a transaction with stripErrorMessagesEnabled.")]
        public void Test_NoticeError_WithException_StripErrorMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage));
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);
            Assert.AreEqual(_compositeTestAgent.TransactionTraces.First().Guid, errorTrace.Guid);

            var expectedErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
            };

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception when in a transaction without error message in high security mode.")]
        public void Test_NoticeError_WithException_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage));
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);
            Assert.AreEqual(_compositeTestAgent.TransactionTraces.First().Guid, errorTrace.Guid);

            var expectedErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "request.uri", Value = "/Unknown"}
            };

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAttributes, AttributeClassification.AgentAttributes, errorTrace);

            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception when outside of a transaction.")]
        public void Test_NoticeErrorOutsideTransaction_WithException()
        {
            // ACT
            AgentApi.NoticeError(new Exception(ExceptionMessage));
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            var errorEvent = _compositeTestAgent.ErrorEvents.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(errorTrace.Path, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);

            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }

        [Test]
        [Description("Verifies a reported error with an Exception when outside of a transaction with StripExceptionMessages enabled.")]
        public void Test_NoticeErrorOutsideTransaction_WithException_StripExceptionMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError(new Exception(ExceptionMessage));
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }


        [Test]
        [Description("Verifies a reported error with an Exception when outside of a transaction without error message in high security mode.")]
        public void Test_NoticeErrorOutsideTransaction_WithException_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError(new Exception(ExceptionMessage));
            _compositeTestAgent.Harvest();

            // ASSERT
            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            var errorEvent = _compositeTestAgent.ErrorEvents.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(errorTrace.Path, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.UserAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);

            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when in a transaction.")]
        public void Test_NoticeError_WithExceptionAndCustomParams()
        {
            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string> { { "attribute1", "value1" } });
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedErrorUserAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var unexpectedNonErrorAttributes = new List<string>
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

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = ExceptionMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when in a transaction with StripExceptionMessages enabled.")]
        public void Test_NoticeError_WithExceptionAndCustomParams_StripExceptionMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string> { { "attribute1", "value1" } });
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedErrorUserAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var unexpectedNonErrorAttributes = new List<string>
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

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters but without an error message when in a transaction in high security.")]
        public void Test_NoticeError_WithExceptionAndCustomParams_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string> { { "attribute1", "value1" } });
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedErrorUserAttributes = new List<string>
            {
                "attribute1"
            };

            var unexpectedNonErrorAttributes = new List<string>
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

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedErrorUserAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "System.Exception" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction.")]
        public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_String()
        {
            var dtmNow = DateTime.Now;

            // ACT
            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string>() {
                { "attribute4", "test" },
            });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute4", Value = "test" }
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }



        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction.")]
        public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_Object()
        {
            var dtmNow = DateTime.Now;

            // ACT
            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, object>() {
                { "attribute1", 21 },
                { "attribute2", dtmNow },
                { "attribute3", TimeSpan.FromMilliseconds(1234) },
                { "attribute4", "test" },
            });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = 21L},
                new ExpectedAttribute {Key = "attribute2", Value = dtmNow.ToString("o")},
                new ExpectedAttribute {Key = "attribute3", Value = 1.234D },
                new ExpectedAttribute {Key = "attribute4", Value = "test" },
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction with StripExceptionMessages.")]
        public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_StripExceptionMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string>() { { "attribute1", "value1" } });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }

        [Test]
        [Description("Verifies a reported error with an Exception and a dictionary of custom parameters when outside of a transaction without error message in high security.")]
        public void Test_NoticeErrorOutsideTransaction_WithExceptionAndCustomParams_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError(new Exception(ExceptionMessage), new Dictionary<string, string>() { { "attribute1", "value1" } });
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedAttributes = new List<string>
            {
                "attribute1"
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("System.Exception", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
        }

        [Test]
        [Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction.")]
        public void Test_NoticeError_WithMessageAndCustomParams()
        {
            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, string>() { { "attribute1", "value1" } });
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedUserAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var unexpectedNonErrorAttributes = new List<string>
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

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedUserAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "Custom Error" },
                new ExpectedAttribute { Key = "error.message", Value = ExceptionMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction with StripExceptionMessages enabled.")]
        public void Test_NoticeError_WithMessageAndCustomParams_StripExceptionMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, string>() { { "attribute1", "value1" } });
            segment.End();
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

            var unexpectedNonErrorAttributes = new List<string>
            {
                "attribute1"
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();
            var transactionTrace = _compositeTestAgent.TransactionTraces.First();
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedUserAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "Custom Error" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error without an error message or custom parameters when in a transaction in high security.")]
        public void Test_NoticeError_WithMessageAndCustomParams_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, string>() { { "attribute1", "value1" } });
            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedAttributes = new List<string>
            {
                "attribute1"
            };

            var unexpectedNonErrorAttributes = new List<string>
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

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorTrace.Path);

            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedErrorAgentAttributes, AttributeClassification.AgentAttributes, errorTrace);
            ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);
            TransactionTraceAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionTrace);
            TransactionEventAssertions.DoesNotHaveAttributes(unexpectedNonErrorAttributes, AttributeClassification.UserAttributes, transactionEvent);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
            Assert.AreEqual("WebTransaction/ASP/TransactionName", errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(segment.SpanId, errorEvent.IntrinsicAttributes()["spanId"]);

            var spanEvents = _compositeTestAgent.SpanEvents.ToArray();
            Assert.AreEqual(2, spanEvents.Length);

            var expectedSpanErrorAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute { Key = "error.class", Value = "Custom Error" },
                new ExpectedAttribute { Key = "error.message", Value = StripExceptionMessagesMessage },
            };

            var spanWithError = spanEvents[1];
            SpanAssertions.HasAttributes(expectedSpanErrorAttributes, AttributeClassification.AgentAttributes, spanWithError);
        }

        [Test]
        [Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction when outside of a transaction.")]
        public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_String()
        {
            // ACT
            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, string>() { { "attribute1", "value1" } });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
        }


        [Test]
        [Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction when outside of a transaction.")]
        public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_Object()
        {
            var dtmNow = DateTime.Now;

            // ACT
            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, object>() {
                { "attribute1", 21 },
                { "attribute2", dtmNow },
                { "attribute3", TimeSpan.FromMilliseconds(1234) },
                { "attribute4", "test" },
            });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = 21L},
                new ExpectedAttribute {Key = "attribute2", Value = dtmNow.ToString("o")},
                new ExpectedAttribute {Key = "attribute3", Value = 1.234D },
                new ExpectedAttribute {Key = "attribute4", Value = "test" },
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(ExceptionMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(ExceptionMessage, errorEvent.IntrinsicAttributes()["error.message"]);
        }


        [Test]
        [Description("Verifies a reported error with a string and a dictionary of custom parameters when in a transaction when outside of a transaction with StripExceptionMessagesEnabled.")]
        public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_StripExceptionMessages()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.stripExceptionMessages.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError("This is an exception string.", new Dictionary<string, string>() { { "attribute1", "value1" } });
            _compositeTestAgent.Harvest();

            // ASSERT
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "attribute1", Value = "value1"}
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceHasAttributes(expectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
        }

        [Test]
        [Description("Verifies a reported error without an error message or custom parameters when in a transaction when outside of a transaction in high security.")]
        public void Test_NoticeErrorOutsideTransaction_WithMessageAndCustomParams_HighSecurity()
        {
            // ACT
            _compositeTestAgent.LocalConfiguration.highSecurity.enabled = true;
            _compositeTestAgent.PushConfiguration();

            AgentApi.NoticeError(ExceptionMessage, new Dictionary<string, string>() { { "attribute1", "value1" } });
            _compositeTestAgent.Harvest();

            // ASSERT
            var unexpectedAttributes = new List<string>
            {
                "attribute1"
            };

            var errorTrace = _compositeTestAgent.ErrorTraces.First();

            Assert.AreEqual("Custom Error", errorTrace.ExceptionClassName);
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorTrace.Path);
            Assert.AreEqual(StripExceptionMessagesMessage, errorTrace.Message);
            Assert.IsEmpty(errorTrace.Attributes.AgentAttributes);
            Assert.IsEmpty(errorTrace.Attributes.Intrinsics);
            ErrorTraceAssertions.ErrorTraceDoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, errorTrace);

            var errorEvent = _compositeTestAgent.ErrorEvents.First();
            Assert.AreEqual(NoticeErrorPathOutsideTransaction, errorEvent.IntrinsicAttributes()["transactionName"]);
            Assert.AreEqual(StripExceptionMessagesMessage, errorEvent.IntrinsicAttributes()["error.message"]);
        }

        [Test]
        [Description("Verifies the metrics used for calculating error % are recorded.")]
        public void Test_NoticeError_ErrorMetrics()
        {
            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();
            AgentApi.NoticeError(new Exception("This is the first exception."), new Dictionary<string, string>() { { "attribute1", "value1" } });
            AgentApi.NoticeError(new Exception("This is the second exception."), new Dictionary<string, string>() { { "attribute2", "value2" } });
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
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment").End();
            AgentApi.NoticeError(new Exception(ExceptionMessage));
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var expectedAttributes = new List<ExpectedAttribute>
            {
                new ExpectedAttribute {Key = "errorType", Value = "System.Exception"},
                new ExpectedAttribute {Key = "errorMessage", Value = ExceptionMessage},
                new ExpectedAttribute {Key = "type", Value = "Transaction"},
                new ExpectedAttribute {Key = "name", Value = "WebTransaction/ASP/TransactionName"}
            };

            TransactionEventAssertions.HasAttributes(expectedAttributes, AttributeClassification.Intrinsics, transactionEvent);
        }

        #endregion

        #region SetTransactionName

        [Test]
        [Description("Verifies a custom transaction name creates supportability and web transaction metrics.")]
        public void Test_SetTransactionName()
        {
            // ARRANGE
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.SetWebTransactionName(WebTransactionType.Action, "priority-1", (TransactionNamePriority)(-1));
            transaction.SetWebTransactionName(WebTransactionType.Action, "priority0", (TransactionNamePriority)(0));
            transaction.SetWebTransactionName(WebTransactionType.Action, "priority6", TransactionNamePriority.FrameworkHigh);
            AgentApi.SetTransactionName("MyCategory", "MyTransaction");
            transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority-1", (TransactionNamePriority)(-1));
            transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority0", (TransactionNamePriority)(0));
            transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority6", TransactionNamePriority.FrameworkHigh);
            transaction.SetWebTransactionName(WebTransactionType.Action, "anotherPriority8", (TransactionNamePriority)(8));
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
            segment.End();
            transaction.SetWebTransactionName(WebTransactionType.Action, "UserPriority", TransactionNamePriority.UserTransactionName);
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
            segment.End();
            AgentApi.SetTransactionName("MyCategory", "MyTransaction");
            transaction.SetWebTransactionName(WebTransactionType.Action, "UserPriority", TransactionNamePriority.UserTransactionName);
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
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

        #region SetTransactionUri

        [Test]
        [Description("Verifies a custom transaction uri creates supportability and web transaction metrics.")]
        public void Test_SetTransactionUri()
        {
            // ARRANGE
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
            segment.End();
            AgentApi.SetTransactionName("MyCategory", "MyTransaction");
            var firstUri = new Uri("http://localhost/first");
            AgentApi.SetTransactionUri(firstUri);
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var actualMetrics = _compositeTestAgent.Metrics;
            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            var expectedMetrics = new List<ExpectedTimeMetric>
            {
                new ExpectedTimeMetric {Name = "WebTransaction/MyCategory/MyTransaction", CallCount = 1},
                new ExpectedTimeMetric {Name = "Supportability/ApiInvocation/SetTransactionUri", CallCount = 1}
            };

            MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
        }

        [Test]
        [Description("Verifies that, with multiple SetTransactionUri calls, the first call takes effect.")]
        public void Test_SetTransactionUri_MultipleCalls_FirstCallWins()
        {
            // ARRANGE
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");
            segment.End();
            var firstUri = new Uri("http://localhost/first");
            var secondUri = new Uri("http://localhost/second");
            AgentApi.SetTransactionUri(firstUri);
            AgentApi.SetTransactionUri(secondUri);
            transaction.End();
            _compositeTestAgent.Harvest();

            // ASSERT
            var actualMetrics = _compositeTestAgent.Metrics;
            var transactionTrace = _compositeTestAgent.TransactionTraces.FirstOrDefault();
            var expectedMetrics = new List<ExpectedTimeMetric>
            {
                new ExpectedTimeMetric {Name = "Supportability/ApiInvocation/SetTransactionUri", CallCount = 2}
            };

            MetricAssertions.MetricsExist(expectedMetrics, actualMetrics);
        }

        #endregion

        #region SetUserParameters

        [Test]
        [Description("Verifies user parameters set via the 'SetUserParameters' API method.")]
        public void Test_SetUserParameters()
        {
            // ARRANGE
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
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

            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
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
            var agentWrapperApi = _compositeTestAgent.GetAgent();

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
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
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
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
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
            segment.End();

            var firstHeader = AgentApi.GetBrowserTimingHeader();
            var secondHeader = AgentApi.GetBrowserTimingHeader();
            var thirdHeader = AgentApi.GetBrowserTimingHeader();

            transaction.End();


            Assert.AreNotEqual(string.Empty, firstHeader);
            Assert.AreEqual(string.Empty, secondHeader);
            Assert.AreEqual(string.Empty, thirdHeader);
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
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment").End();
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
            var agentVersion = AgentInstallConfiguration.AgentVersion;

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

        #region SetErrorGroupCallback

        [Test]
        public void Test_SetErrorGroupCallback()
        {
            Func<IReadOnlyDictionary<string, object>, string> myCallback = ex => "my error group";

            AgentApi.SetErrorGroupCallback(myCallback);

            _compositeTestAgent.PushConfiguration();
            var errorGroupCallback = _compositeTestAgent.CurrentConfiguration.ErrorGroupCallback;

            Assert.AreSame(myCallback, errorGroupCallback);
        }

        #endregion

        #region GetRequestMetadata

        [Test]
        public void Test_GetRequestMetadata()
        {
            var NewRelicIdHttpHeader = "X-NewRelic-ID";
            var TransactionDataHttpHeader = "X-NewRelic-Transaction";

            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;
            _compositeTestAgent.ServerConfiguration.EncodingKey = "foo";
            _compositeTestAgent.PushConfiguration();

            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");
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

            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;
            var trustedAccount = long.Parse(_compositeTestAgent.ServerConfiguration.CatId.Split(new[] { '#' })[0]);
            _compositeTestAgent.ServerConfiguration.TrustedIds = new long[] { trustedAccount };
            _compositeTestAgent.ServerConfiguration.EncodingKey = "foo";
            _compositeTestAgent.PushConfiguration();

            // ACT
            var transaction = _compositeTestAgent.GetAgent().CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.ASP),
                transactionDisplayName: "TransactionName",
                doNotTrackAsUnitOfWork: true);
            transaction.SetHttpResponseStatusCode(300);
            var segment = _compositeTestAgent.GetAgent().StartTransactionSegmentOrThrow("segment");

            var headers = AgentApi.GetRequestMetadata();
            _compositeTestAgent.GetAgent().CurrentTransaction.AcceptDistributedTraceHeaders(headers, GetHeaderValue, TransportType.HTTP);

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
                () => Assert.AreEqual("WebTransaction/StatusCode/300", crossApplicationResponseData.TransactionName),
                () => Assert.NotNull(crossApplicationResponseData.QueueTimeInSeconds),
                () => Assert.IsTrue(crossApplicationResponseData.ResponseTimeInSeconds > 0),
                () => Assert.NotNull(crossApplicationResponseData.ContentLength),
                () => Assert.NotNull(crossApplicationResponseData.TransactionGuid)
                );

            IEnumerable<string> GetHeaderValue(IEnumerable<KeyValuePair<string, string>> carrier, string key)
            {
                if (carrier != null)
                {
                    var headerValues = new List<string>();
                    foreach (var item in carrier)
                    {
                        if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                        {
                            headerValues.Add(item.Value);
                        }
                    }
                    return headerValues;
                }

                return null;
            }
        }
        #endregion

        #region InitializePublicAgent

        [Test]
        public void Test_InitializePublicAgent()
        {
            // ARRANGE
            var agent = new DummyAgent();

            //ACT
            AgentApi.InitializePublicAgent(agent);

            //ASSERT
            Assert.NotNull(agent.WrappedAgent);
            Assert.IsInstanceOf<AgentBridgeApi>(agent.WrappedAgent);
        }

        public class DummyAgent
        {
            public object WrappedAgent = null;
            internal void SetWrappedAgent(object wrappedAgent)
            {
                WrappedAgent = wrappedAgent;
            }
        }

        #endregion

        #region TraceMetadata

        [Test]
        public void Test_TraceMetadataReturnsValidValues()
        {
            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var transaction = agentWrapperApi.CreateTransaction(true, WebTransactionType.ASP.ToString(), "TransactionName", false);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");

            dynamic traceMetadata = agentWrapperApi.TraceMetadata;
            var traceId = traceMetadata.TraceId;
            var spanId = traceMetadata.SpanId;
            var isSampled = traceMetadata.IsSampled;

            segment.End();
            transaction.End();
            _compositeTestAgent.Harvest();

            var transactionEvent = _compositeTestAgent.TransactionEvents.First();
            var transactionAttributes = transactionEvent.IntrinsicAttributes();

            NrAssert.Multiple(
                () => Assert.AreEqual(transactionAttributes["traceId"], traceId),
                () => Assert.AreEqual(segment.SpanId, spanId),
                () => Assert.AreEqual(transactionAttributes["sampled"], isSampled)
            );
        }

        [Test]
        public void TraceMetadataReturnsEmptyValuesIfDTDisabled()
        {
            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;
            _compositeTestAgent.PushConfiguration();

            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var transaction = agentWrapperApi.CreateTransaction(true, WebTransactionType.ASP.ToString(), "TransactionName", false);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");

            dynamic traceMetadata = agentWrapperApi.TraceMetadata;
            var traceId = traceMetadata.TraceId;
            var spanId = traceMetadata.SpanId;
            var isSampled = traceMetadata.IsSampled;

            NrAssert.Multiple(
                () => Assert.AreEqual(string.Empty, traceId),
                () => Assert.AreEqual(string.Empty, spanId),
                () => Assert.AreEqual(false, isSampled)
            );
        }

        #endregion TraceMetadata

        #region GetLinkingMetadata

        [Test]
        public void Test_GetLinkingMetadataOnlyReturnsExistingValues()
        {
            // traceId and spanId are not available if DistributedTracing is disabled
            _compositeTestAgent.LocalConfiguration.distributedTracing.enabled = false;

            _compositeTestAgent.ServerConfiguration.EntityGuid = "entityguid";
            _compositeTestAgent.PushConfiguration();

            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var transaction = agentWrapperApi.CreateTransaction(true, WebTransactionType.ASP.ToString(), "TransactionName", false);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");

            Dictionary<string, string> linkingMetadata = agentWrapperApi.GetLinkingMetadata();

            NrAssert.Multiple(
                () => Assert.IsFalse(linkingMetadata.ContainsKey("trace.id"), "Key trace.id should not be found"),
                () => Assert.IsFalse(linkingMetadata.ContainsKey("span.id"), "Key span.id should not be found"),
                () => Assert.AreEqual(linkingMetadata["entity.type"], "SERVICE"),
                () => Assert.AreEqual(linkingMetadata["entity.guid"], "entityguid"),
                () => Assert.IsTrue(linkingMetadata.ContainsKey("hostname"), "Key hostname not found"),
                () => Assert.IsNotEmpty(linkingMetadata["hostname"], "Key hostname was empty")
            );
        }

        [Test]
        public void Test_GetLinkingMetadataReturnsValidValuesIfDTEnabled()
        {
            _compositeTestAgent.ServerConfiguration.EntityGuid = "entityguid";
            _compositeTestAgent.PushConfiguration();

            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var transaction = agentWrapperApi.CreateTransaction(true, WebTransactionType.ASP.ToString(), "TransactionName", false);
            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");

            Dictionary<string, string> linkingMetadata = agentWrapperApi.GetLinkingMetadata();

            NrAssert.Multiple(
                () => Assert.IsTrue(linkingMetadata.ContainsKey("trace.id"), "Key trace.id not found"),
                () => Assert.IsNotEmpty(linkingMetadata["trace.id"]),
                () => Assert.AreEqual(linkingMetadata["span.id"], segment.SpanId),
                () => Assert.AreEqual(linkingMetadata["entity.type"], "SERVICE"),
                () => Assert.AreEqual(linkingMetadata["entity.guid"], "entityguid"),
                () => Assert.IsTrue(linkingMetadata.ContainsKey("hostname"), "Key hostname not found"),
                () => Assert.IsNotEmpty(linkingMetadata["hostname"], "Key hostname is empty")
            );
        }

        #endregion GetLinkingMetadataCATS

        #region Span Custom Attributes

        [Test]
        public void SpanCustomAttributes()
        {
            var agentWrapperApi = _compositeTestAgent.GetAgent();
            var dtm1 = DateTime.Now;
            var dtm2 = DateTimeOffset.Now;

            // ACT
            var transaction = agentWrapperApi.CreateTransaction(
                isWeb: true,
                category: EnumNameCache<WebTransactionType>.GetName(WebTransactionType.Action),
                transactionDisplayName: "name",
                doNotTrackAsUnitOfWork: true);

            var segment = agentWrapperApi.StartTransactionSegmentOrThrow("segment");

            segment.AddCustomAttribute("key1", "val1");
            segment.AddCustomAttribute("key2", 2.0d);
            segment.AddCustomAttribute("key3", 3.1d);
            segment.AddCustomAttribute("key4", 4.0f);
            segment.AddCustomAttribute("key5", true);
            segment.AddCustomAttribute("key6", dtm1);
            segment.AddCustomAttribute("key7", dtm2);
            segment.AddCustomAttribute("key8", null);
            segment.AddCustomAttribute("", dtm2);

            var singleStringValue = new StringValues("avalue");
            var multiStringValue = new StringValues(new[] { "onevalue", "twovalue", "threevalue" });
            segment.AddCustomAttribute("key9a", singleStringValue);
            segment.AddCustomAttribute("key9b", multiStringValue);

            var expectedAttributes = new[]
            {
                new ExpectedAttribute(){ Key = "key1", Value = "val1"},
                new ExpectedAttribute(){ Key = "key2", Value = 2.0d},
                new ExpectedAttribute(){ Key = "key3", Value = 3.1d},
                new ExpectedAttribute(){ Key = "key4", Value = 4.0d},
                new ExpectedAttribute(){ Key = "key5", Value = true},
                new ExpectedAttribute(){ Key = "key6", Value = dtm1.ToString("o")},
                new ExpectedAttribute(){ Key = "key7", Value = dtm2.ToString("o")},
                new ExpectedAttribute(){ Key = "key9a", Value = "avalue"},
                new ExpectedAttribute(){ Key = "key9b", Value = "onevalue,twovalue,threevalue"}
            };

            var unexpectedAttributes = new[]
            {
                "key8",
                string.Empty
            };

            segment.End();
            transaction.End();

            _compositeTestAgent.Harvest();

            var allSpans = _compositeTestAgent.SpanEvents;
            var testSpan = allSpans.LastOrDefault();

            NrAssert.Multiple
            (
                () => Assert.AreEqual(2, allSpans.Count),
                () => SpanAssertions.HasAttributes(expectedAttributes, AttributeClassification.UserAttributes, testSpan),
                () => SpanAssertions.DoesNotHaveAttributes(unexpectedAttributes, AttributeClassification.UserAttributes, testSpan)
            );
        }

        #endregion
    }
}
