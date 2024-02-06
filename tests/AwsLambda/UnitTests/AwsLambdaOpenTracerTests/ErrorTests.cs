// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda;
using NewRelic.Core.DistributedTracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using System;
using System.Collections.Generic;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class ErrorTests
    {
        private MockLogger _logger;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _logger = new MockLogger();
        }

        [Test]
        public void TestDistributedTraceInstrinsicsShowUpIn_ErrorEvents_ErrorTraces_SpanEvent_AnalyticEvent()
        {
            var dtPayload = new DistributedTracePayload
            {
                Type = "App",
                AccountId = "test-accountid",
                AppId = "test-appid",
                Guid = "test-guid",
                TraceId = "test-traceid",
                TrustKey = "test-trustkey",
                Priority = 1.0F,
                Sampled = true,
                Timestamp = DateTime.Now,
                TransactionId = "test-transactionid",
            };

            var tags = new Dictionary<string, string>
            {
                { "newrelic", dtPayload.SerializeAndEncodeDistributedTracePayload() }
            };

            var tracer = LambdaTracer.Instance;

            var lambdaPayloadContext = (LambdaPayloadContext)tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(tags));

            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", logger: _logger);

            span.RootSpan.DistributedTracingState.SetInboundDistributedTracePayload(lambdaPayloadContext.GetPayload());
            span.RootSpan.DistributedTracingState.SetTransportDurationInMillis(1000);
            span.RootSpan.PrioritySamplingState.Priority = (span.RootSpan.DistributedTracingState.InboundPayload.Priority.HasValue)
                                                        ? span.RootSpan.DistributedTracingState.InboundPayload.Priority.Value
                                                        : LambdaTracer.TracePriorityManager.Create();
            span.RootSpan.PrioritySamplingState.Sampled = span.RootSpan.DistributedTracingState.InboundPayload.Sampled.Value;

            var exception = new CustomException("my exception.", "this is a stack trace.");

            var errorAttributes = CreateSpanErrorAttributes(exception, exception.Message, exception.StackTrace);

            span.Log(errorAttributes);

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(_logger.LastLogMessage);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            Assert.Multiple(() =>
            {
                Assert.That(data, Does.Contain("analytic_event_data"));
                Assert.That(data, Does.Contain("\"error\":true"));
                Assert.That(data, Does.Contain("\"error_event_data\":["));
                Assert.That(data, Does.Contain("\"error_data\":[null,[["));

                Assert.That(TestUtil.CountStringOccurrences(data, "\"parent.type\":\"App\""), Is.EqualTo(4));
                Assert.That(TestUtil.CountStringOccurrences(data, "\"parent.account\":\"test-accountid\""), Is.EqualTo(4));
                Assert.That(TestUtil.CountStringOccurrences(data, "\"parent.app\":\"test-appid\""), Is.EqualTo(4));
                Assert.That(TestUtil.CountStringOccurrences(data, "\"parent.transportType\":\"Unknown\""), Is.EqualTo(4));
                Assert.That(TestUtil.CountStringOccurrences(data, "\"parent.transportDuration\":1000.0"), Is.EqualTo(4));
            });
        }

        [Test]
        public void TestErrorEventsAndErrorTracesShowUpInPayload()
        {
            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", logger: _logger);

            var exception = new CustomException("my exception.", "this is a stack trace.");

            var errorAttributes = CreateSpanErrorAttributes(exception, exception.Message, exception.StackTrace);

            span.Log(errorAttributes);

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(_logger.LastLogMessage);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            var deserializedData = JsonConvert.DeserializeObject<JObject>(data);

            var spanTraceId = span.Intrinsics["traceId"];
            var guidInErrorData = deserializedData["error_data"][1][0][4]["intrinsics"]["guid"].ToString();
            var guidInErrorEventData = deserializedData["error_event_data"][2][0][0]["guid"].ToString();

            Assert.Multiple(() =>
            {
                Assert.That(guidInErrorData, Is.EqualTo(spanTraceId), "guid in error data should match the traceId of the span");
                Assert.That(guidInErrorEventData, Is.EqualTo(spanTraceId), "guid in error event data should match the traceId of the span");

                Assert.That(data, Does.Contain("analytic_event_data"));
                Assert.That(data, Does.Contain("\"error\":true"));

                Assert.That(data, Does.Contain("\"error_event_data\":["));
                Assert.That(data, Does.Contain("\"type\":\"TransactionError\""));
                Assert.That(data, Does.Contain("\"error.class\":\"CustomException\""));
                Assert.That(data, Does.Contain("\"error.message\":\"my exception.\""));
                Assert.That(data, Does.Contain("{\"error.kind\":\"Exception\"}"));

                Assert.That(data, Does.Contain("\"error_data\":[null,[["));
                Assert.That(data, Does.Contain("{\"stack_trace\":[\"this is a stack trace.\"],"));
            });

        }

        [Test]
        public void IfErrorMessageIsNull_Or_StackTraceIsNull_FallbackToExceptionObjectMessage()
        {
            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", logger: _logger);

            var exception = new CustomException("my exception.", "this is a stack trace.");
            var errorAttributes = CreateSpanErrorAttributes(exception, null, null);

            span.Log(errorAttributes);

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(_logger.LastLogMessage);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            Assert.Multiple(() =>
            {
                Assert.That(data, Does.Contain("error_event_data"));
                Assert.That(data, Does.Contain("\"type\":\"TransactionError\""));
                Assert.That(data, Does.Contain("\"error.class\":\"CustomException\""));
                Assert.That(data, Does.Contain("\"error.message\":\"my exception.\""));
                Assert.That(data, Does.Contain("{\"error.kind\":\"Exception\"}"));

                Assert.That(data, Does.Contain("\"error_data\":[null,[["));
                Assert.That(data, Does.Contain("{\"stack_trace\":[\"this is a stack trace.\"],"));
            });
        }

        [Test]
        public void NoErrorEventAndNoErrorData_IfNoExceptionMessageAndNoExceptionObject()
        {
            var startTime = DateTimeOffset.UtcNow;
            var span = TestUtil.CreateRootSpan("operationName", startTime, new Dictionary<string, object>(), "testGuid", logger: _logger);

            var errorAttributes = CreateSpanErrorAttributes("NonException", null, null);

            span.Log(errorAttributes);

            span.Finish();

            var deserializedPayload = JsonConvert.DeserializeObject<object[]>(_logger.LastLogMessage);
            var data = TestUtil.DecodeAndDecompressNewRelicPayload(deserializedPayload[3] as string);

            Assert.Multiple(() =>
            {
                Assert.That(data.Contains("\"error_event_data\":["), Is.False);
                Assert.That(data.Contains("\"type\":\"TransactionError\""), Is.False);
                Assert.That(data.Contains("\"error.class\":\"CustomException\""), Is.False);
                Assert.That(data.Contains("\"error.message\":\"my exception.\""), Is.False);
                Assert.That(data.Contains("{\"error.kind\":\"Exception\"}"), Is.False);

                Assert.That(data.Contains("\"error_data\":[null,[["), Is.False);
                Assert.That(data.Contains("{\"stack_trace\":[\"this is a stack trace.\"],"), Is.False);
            });
        }

        private IDictionary<string, object> CreateSpanErrorAttributes(object exception, string message, string stackTrace)
        {
            var errorAttributes = new Dictionary<string, object>();
            errorAttributes.Add("event", Tags.Error.Key);
            errorAttributes.Add("error.object", exception);
            errorAttributes.Add("error.kind", "Exception");
            errorAttributes.Add("message", message);
            errorAttributes.Add("stack", stackTrace);
            return errorAttributes;
        }

        class CustomException : Exception
        {
            private readonly string _stackTrace;

            public CustomException(string message, string stackTrace) : base(message)
            {
                _stackTrace = stackTrace;
            }

            public override string StackTrace => _stackTrace;
        }
    }
}
