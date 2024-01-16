// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core.DistributedTracing;
using NewRelic.Agent.TestUtilities;
using OpenTracing.Propagation;
using System;
using System.Threading;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class LambdaSpanBuilderTests
    {
        private DistributedTracePayload _dtPayload;
        private IDictionary<string, string> _tags;

        private const int ExpectedGuidLength = 16;

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
                TransactionId = "test-transactionid"
            };

            _tags = new Dictionary<string, string>
            {
                { "newrelic", _dtPayload.SerializeAndEncodeDistributedTracePayload() }
            };
        }

        [Test]
        public void BuildActiveRootSpanWith_StartActive()
        {
            var tracer = LambdaTracer.Instance;
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name);
            LambdaRootSpan span = null;
            LambdaRootSpan activeSpan = null;
            using (var scope = spanBuilder.StartActive())
            {
                span = (LambdaRootSpan)scope.Span;
                activeSpan = (LambdaRootSpan)tracer.ActiveSpan;
            }

            Thread.Sleep(500);
            span.Finish();

            ClassicAssert.NotNull(span, "span must not be null");
            ClassicAssert.AreEqual(ExpectedGuidLength, span.Guid().Length, $"span guid length must be {ExpectedGuidLength}");
            ClassicAssert.AreSame(activeSpan, span, "The span we created should be the active span");
            ClassicAssert.AreEqual(name, span.GetOperationName(), $"span operation name should be {name} but got {span.GetOperationName()} instead");
            ClassicAssert.IsTrue(span.GetDurationInSeconds() > 0.0, $"span duration must be greater than 0 but got {span.GetDurationInSeconds()} instead");
            ClassicAssert.IsFalse(string.IsNullOrEmpty(span.Guid()), "span guid must not be null or empty");
        }

        [Test]
        public void BuildRootSpanWith_Start()
        {
            var tracer = LambdaTracer.Instance;
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name);
            LambdaRootSpan span1 = (LambdaRootSpan)spanBuilder.Start();
            LambdaRootSpan span2 = (LambdaRootSpan)spanBuilder.Start();

            ClassicAssert.IsNull(tracer.ActiveSpan);
            Thread.Sleep(500);

            span1.Finish();
            span2.Finish();

            ClassicAssert.NotNull(span1, "span1 must not be null");
            ClassicAssert.AreEqual(ExpectedGuidLength, span1.Guid().Length, $"span1 guid length must be {ExpectedGuidLength}");
            ClassicAssert.AreEqual(name, span1.GetOperationName(), $"span1 operation name should be {name} but got {span1.GetOperationName()} instead");
            ClassicAssert.IsTrue(span1.GetDurationInSeconds() > 0.0, $"span1 duration must be greater than 0 but got {span1.GetDurationInSeconds()} instead");
            ClassicAssert.IsFalse(string.IsNullOrEmpty(span1.Guid()), "span1 guid must not be null or empty");

            ClassicAssert.NotNull(span2, "span2 must not be null");
            ClassicAssert.AreEqual(ExpectedGuidLength, span2.Guid().Length, $"span2 guid length must be {ExpectedGuidLength}");
            ClassicAssert.AreEqual(name, span2.GetOperationName(), $"span2 operation name should be {name} but got {span2.GetOperationName()} instead");
            ClassicAssert.IsTrue(span2.GetDurationInSeconds() > 0.0, $"span2 duration must be greater than 0 but got {span2.GetDurationInSeconds()} instead");
            ClassicAssert.IsFalse(string.IsNullOrEmpty(span2.Guid()), "span2 guid must not be null or empty");

            ClassicAssert.AreNotEqual(span1.Guid(), span2.Guid(), "span1 and span2 must have different guids");
        }

        [Test]
        public void BuildChildSpanWith_Start()
        {
            var tracer = LambdaTracer.Instance;
            var rootName = "rootOperationName";
            var rootSpanBuilder = tracer.BuildSpan(rootName);
            LambdaRootSpan rootSpan = (LambdaRootSpan)rootSpanBuilder.Start();

            var childName = "childOperationName";
            var childSpanBuilder = tracer.BuildSpan(childName).AsChildOf(rootSpan);
            LambdaSpan childSpan = (LambdaSpan)childSpanBuilder.Start();

            ClassicAssert.AreEqual(rootSpan.Guid(), childSpan.RootSpan.Guid(), "The guid of the child span's RootSpan should match the guid of the root span");
        }

        [Test]
        public void TracerId_DoesNotMatch_TransactionId_WithDistributedTracePayload()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name).AsChildOf(contextTextMap);
            var span = (LambdaRootSpan)spanBuilder.Start();
            span.Finish();

            Assert.That(span.Intrinsics.ContainsKey("traceId"), Is.True);
            Assert.That(span.Intrinsics["traceId"].IsEqualTo(_dtPayload.TraceId));
            Assert.That(span.Intrinsics["traceId"].IsNotEqualTo(span.TransactionState.TransactionId));
        }

        [Test]
        public void TracerId_DoesMatch_TransactionId_WithoutDistributedTracePayload()
        {
            var tracer = LambdaTracer.Instance;
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name);
            var span = (LambdaRootSpan)spanBuilder.Start();
            span.Finish();

            Assert.That(span.Intrinsics.ContainsKey("traceId"), Is.True);
            Assert.That(span.Intrinsics["traceId"].IsNotEqualTo(_dtPayload.TraceId));
            Assert.That(span.Intrinsics["traceId"].IsEqualTo(span.TransactionState.TransactionId));
        }

        [Test]
        public void Priority_DoesMatch_IncomingPayloadPriority()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name).AsChildOf(contextTextMap);
            var span = (LambdaSpan)spanBuilder.Start();
            span.Finish();

            Assert.That(span.Intrinsics.ContainsKey("priority"), Is.True);
            Assert.That(span.Intrinsics["priority"], Is.EqualTo(_dtPayload.Priority));
        }

        [Test]
        public void Priority_Exists_WithNoIncomingPayload()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name).AsChildOf(contextTextMap);
            var span = (LambdaSpan)spanBuilder.Start();
            span.Finish();

            Assert.That(span.Intrinsics.ContainsKey("priority"), Is.True);
            Assert.That(span.Intrinsics["priority"], Is.GreaterThanOrEqualTo(0));
            Assert.That(span.Intrinsics["priority"], Is.LessThanOrEqualTo(1));
        }

        [Test]
        public void Sampled_Exists_WithNoIncomingPayload()
        {
            var tracer = LambdaTracer.Instance;
            var contextTextMap = tracer.Extract(BuiltinFormats.TextMap, new TextMapExtractAdapter(_tags));
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name).AsChildOf(contextTextMap);
            var span = (LambdaSpan)spanBuilder.Start();
            span.Finish();

            Assert.That(span.Intrinsics.ContainsKey("sampled"), Is.True);
            Assert.That(span.Intrinsics["sampled"], Is.True);
        }
    }
}
