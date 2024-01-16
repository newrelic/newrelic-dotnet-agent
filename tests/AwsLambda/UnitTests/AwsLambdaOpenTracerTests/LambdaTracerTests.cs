// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using NewRelic.Core.DistributedTracing;
using NewRelic.OpenTracing.AmazonLambda.Util;
using OpenTracing.Propagation;
using System;
using System.Net.Http;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class LambdaTracerTests
    {
        private const string NEWRELIC_TRACE_HEADER = "newrelic";
        private DistributedTracePayload _dtPayload;
        private IDictionary<string, string> _tags;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            // type, accountId, appId, guid, traceId, trustKey, priority, sampled, timestamp, transactionId
            _dtPayload = new DistributedTracePayload
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

            _tags = new Dictionary<string, string>
            {
                { "newrelic", _dtPayload.SerializeAndEncodeDistributedTracePayload() }
            };
        }

        [Test]
        public void IsSingleton()
        {
            var tracer1 = LambdaTracer.Instance;
            var tracer2 = LambdaTracer.Instance;

            ClassicAssert.AreSame(tracer1, tracer2);
        }

        [Test]
        public void Extract_ReturnsNull_FromEmptyTags()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(new Dictionary<string, string>()));
            var contextHttpHeaders = tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(new Dictionary<string, string>()));
            var payloadMap = tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(string.Empty));
            Assert.That(contextTextMap, Is.Null);
            Assert.That(contextHttpHeaders, Is.Null);
            Assert.That(payloadMap, Is.Null);
        }

        [Test]
        public void Extract_ReturnsLambdaPayloadContext_FromPayload()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var contextHttpHeaders = tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(_tags));
            var payloadEncodedMap = tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(_dtPayload.SerializeAndEncodeDistributedTracePayload()));
            var payloadJsonMap = tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(_dtPayload.ToJson()));
            Assert.That(contextTextMap, Is.TypeOf<LambdaPayloadContext>());
            Assert.That(contextHttpHeaders, Is.TypeOf<LambdaPayloadContext>());
            Assert.That(payloadEncodedMap, Is.TypeOf<LambdaPayloadContext>());
            Assert.That(payloadJsonMap, Is.TypeOf<LambdaPayloadContext>());
        }

        [Test]
        public void Extract_ReturnsNull_FromNullTags()
        {
            var tracer = LambdaTracer.Instance;
            Assert.That(() => tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(null)), Throws.Exception.TypeOf<NullReferenceException>());
            Assert.That(() => tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(null)), Throws.Exception.TypeOf<NullReferenceException>());
            Assert.That(() => tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(null)), Throws.Exception.TypeOf<NullReferenceException>());
            Assert.That(() => tracer.Extract(BuiltinFormats.TextMap, null), Throws.Exception.TypeOf<ArgumentNullException>());
            Assert.That(() => tracer.Extract(BuiltinFormats.HttpHeaders, null), Throws.Exception.TypeOf<ArgumentNullException>());
            Assert.That(() => tracer.Extract(NewRelicFormats.Payload, null), Throws.Exception.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Extract_TextMap_ValidatePayloadValue()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var payload = ((LambdaPayloadContext)contextTextMap).GetPayload();
            Assert.That(payload.Type, Is.EqualTo(_dtPayload.Type));
            Assert.That(payload.AccountId, Is.EqualTo(_dtPayload.AccountId));
            Assert.That(payload.AppId, Is.EqualTo(_dtPayload.AppId));
            Assert.That(payload.Guid, Is.EqualTo(_dtPayload.Guid));
            Assert.That(payload.TraceId, Is.EqualTo(_dtPayload.TraceId));
            Assert.That(payload.TrustKey, Is.EqualTo(_dtPayload.TrustKey));
            Assert.That(payload.Priority, Is.EqualTo(_dtPayload.Priority));
            Assert.That(payload.Sampled, Is.EqualTo(_dtPayload.Sampled));
            Assert.That(payload.Timestamp.ToUnixTimeMilliseconds(), Is.EqualTo(_dtPayload.Timestamp.ToUnixTimeMilliseconds()));
            Assert.That(payload.TransactionId, Is.EqualTo(_dtPayload.TransactionId));
        }

        [Test]
        public void Extract_PayloadFormat_Encoded_ValidatePayloadValue()
        {
            var tracer = LambdaTracer.Instance;
            var payloadMap = tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(_dtPayload.SerializeAndEncodeDistributedTracePayload()));
            var payload = ((LambdaPayloadContext)payloadMap).GetPayload();
            Assert.That(payload.Type, Is.EqualTo(_dtPayload.Type));
            Assert.That(payload.AccountId, Is.EqualTo(_dtPayload.AccountId));
            Assert.That(payload.AppId, Is.EqualTo(_dtPayload.AppId));
            Assert.That(payload.Guid, Is.EqualTo(_dtPayload.Guid));
            Assert.That(payload.TraceId, Is.EqualTo(_dtPayload.TraceId));
            Assert.That(payload.TrustKey, Is.EqualTo(_dtPayload.TrustKey));
            Assert.That(payload.Priority, Is.EqualTo(_dtPayload.Priority));
            Assert.That(payload.Sampled, Is.EqualTo(_dtPayload.Sampled));
            Assert.That(payload.Timestamp.ToUnixTimeMilliseconds(), Is.EqualTo(_dtPayload.Timestamp.ToUnixTimeMilliseconds()));
            Assert.That(payload.TransactionId, Is.EqualTo(_dtPayload.TransactionId));
        }

        [Test]
        public void Extract_PayloadFormat_Json_ValidatePayloadValue()
        {
            var tracer = LambdaTracer.Instance;
            var payloadMap = tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter(_dtPayload.ToJson()));
            var payload = ((LambdaPayloadContext)payloadMap).GetPayload();
            Assert.That(payload.Type, Is.EqualTo(_dtPayload.Type));
            Assert.That(payload.AccountId, Is.EqualTo(_dtPayload.AccountId));
            Assert.That(payload.AppId, Is.EqualTo(_dtPayload.AppId));
            Assert.That(payload.Guid, Is.EqualTo(_dtPayload.Guid));
            Assert.That(payload.TraceId, Is.EqualTo(_dtPayload.TraceId));
            Assert.That(payload.TrustKey, Is.EqualTo(_dtPayload.TrustKey));
            Assert.That(payload.Priority, Is.EqualTo(_dtPayload.Priority));
            Assert.That(payload.Sampled, Is.EqualTo(_dtPayload.Sampled));
            Assert.That(payload.Timestamp.ToUnixTimeMilliseconds(), Is.EqualTo(_dtPayload.Timestamp.ToUnixTimeMilliseconds()));
            Assert.That(payload.TransactionId, Is.EqualTo(_dtPayload.TransactionId));
        }

        [Test]
        public void Extract_ThrowsOnInvalidPayloadString()
        {
            var tracer = LambdaTracer.Instance;
            var tags = new Dictionary<string, string>() { { "newrelic", "fred flintstone" } };
            Assert.That(() => tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(tags)), Throws.Exception.TypeOf<ArgumentException>());
            Assert.That(() => tracer.Extract(BuiltinFormats.HttpHeaders, new TextMapExtractAdapter(tags)), Throws.Exception.TypeOf<ArgumentException>());
            Assert.That(() => tracer.Extract(NewRelicFormats.Payload, new PayloadExtractAdapter("I AM BAD")), Throws.Exception.TypeOf<ArgumentException>());
        }

        [Test]
        public void Inject_Dictionary()
        {
            var tracer = LambdaTracer.Instance as LambdaTracer;
            tracer.AccountId = "test-accountId";
            tracer.TrustedAccountKey = "test-accountId";
            var scope = tracer.BuildSpan("stuff").StartActive();
            var headers = new Dictionary<string, string>();
            var injector = new TextMapInjectAdapter(headers);
            tracer.Inject(scope.Span.Context, BuiltinFormats.HttpHeaders, injector);

            Assert.That(headers.Count, Is.EqualTo(1));
            Assert.That(headers.ContainsKey(NEWRELIC_TRACE_HEADER), Is.True);
            Assert.That(headers[NEWRELIC_TRACE_HEADER], Is.Not.Null);
        }

        [Test]
        public void Inject_HttpRequestHeaders()
        {
            var tracer = LambdaTracer.Instance as LambdaTracer;
            tracer.AccountId = "test-accountId";
            tracer.TrustedAccountKey = "test-accountId";
            var scope = tracer.BuildSpan("stuff").StartActive();
            var headers = new HttpClient().DefaultRequestHeaders;
            headers.Clear();
            var injector = new HttpRequestHeadersInjectAdapter(headers);
            tracer.Inject(scope.Span.Context, BuiltinFormats.HttpHeaders, injector);

            Assert.That(headers.Contains(NEWRELIC_TRACE_HEADER), Is.True);
            Assert.That(headers.GetValues(NEWRELIC_TRACE_HEADER), Is.Not.Null);
        }
    }
}
