// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenTracing.Propagation;
using OpenTracing.Tag;
using System;

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

            ClassicAssert.IsTrue(data.Contains("analytic_event_data"));
            ClassicAssert.IsTrue(data.Contains("\"error\":true"));
            ClassicAssert.IsTrue(data.Contains("\"error_event_data\":["));
            ClassicAssert.IsTrue(data.Contains("\"error_data\":[null,[["));

            ClassicAssert.AreEqual(4, TestUtil.CountStringOccurrences(data, "\"parent.type\":\"App\""));
            ClassicAssert.AreEqual(4, TestUtil.CountStringOccurrences(data, "\"parent.account\":\"test-accountid\""));
            ClassicAssert.AreEqual(4, TestUtil.CountStringOccurrences(data, "\"parent.app\":\"test-appid\""));
            ClassicAssert.AreEqual(4, TestUtil.CountStringOccurrences(data, "\"parent.transportType\":\"Unknown\""));
            ClassicAssert.AreEqual(4, TestUtil.CountStringOccurrences(data, "\"parent.transportDuration\":1000.0"));
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


            ClassicAssert.AreEqual(spanTraceId, guidInErrorData, "guid in error data should match the traceId of the span");
            ClassicAssert.AreEqual(spanTraceId, guidInErrorEventData, "guid in error event data should match the traceId of the span");

            ClassicAssert.IsTrue(data.Contains("analytic_event_data"));
            ClassicAssert.IsTrue(data.Contains("\"error\":true"));

            ClassicAssert.IsTrue(data.Contains("\"error_event_data\":["));
            ClassicAssert.IsTrue(data.Contains("\"type\":\"TransactionError\""));
            ClassicAssert.IsTrue(data.Contains("\"error.class\":\"CustomException\""));
            ClassicAssert.IsTrue(data.Contains("\"error.message\":\"my exception.\""));
            ClassicAssert.IsTrue(data.Contains("{\"error.kind\":\"Exception\"}"));

            ClassicAssert.IsTrue(data.Contains("\"error_data\":[null,[["));
            ClassicAssert.IsTrue(data.Contains("{\"stack_trace\":[\"this is a stack trace.\"],"));

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

            ClassicAssert.IsTrue(data.Contains("error_event_data"));
            ClassicAssert.IsTrue(data.Contains("\"type\":\"TransactionError\""));
            ClassicAssert.IsTrue(data.Contains("\"error.class\":\"CustomException\""));
            ClassicAssert.IsTrue(data.Contains("\"error.message\":\"my exception.\""));
            ClassicAssert.IsTrue(data.Contains("{\"error.kind\":\"Exception\"}"));

            ClassicAssert.IsTrue(data.Contains("\"error_data\":[null,[["));
            ClassicAssert.IsTrue(data.Contains("{\"stack_trace\":[\"this is a stack trace.\"],"));
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

            ClassicAssert.IsFalse(data.Contains("\"error_event_data\":["));
            ClassicAssert.IsFalse(data.Contains("\"type\":\"TransactionError\""));
            ClassicAssert.IsFalse(data.Contains("\"error.class\":\"CustomException\""));
            ClassicAssert.IsFalse(data.Contains("\"error.message\":\"my exception.\""));
            ClassicAssert.IsFalse(data.Contains("{\"error.kind\":\"Exception\"}"));

            ClassicAssert.IsFalse(data.Contains("\"error_data\":[null,[["));
            ClassicAssert.IsFalse(data.Contains("{\"stack_trace\":[\"this is a stack trace.\"],"));
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
