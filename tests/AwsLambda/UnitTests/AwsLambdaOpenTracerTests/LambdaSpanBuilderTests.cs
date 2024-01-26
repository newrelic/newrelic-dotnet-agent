// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.OpenTracing.AmazonLambda;
using NewRelic.Core.DistributedTracing;
using NewRelic.Agent.TestUtilities;
using NUnit.Framework;
using OpenTracing.Propagation;
using System;
using System.Threading;
using System.Collections.Generic;

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

            Assert.That(span, Is.Not.Null, "span must not be null");
            Assert.Multiple(() =>
            {
                Assert.That(span.Guid(), Has.Length.EqualTo(ExpectedGuidLength), $"span guid length must be {ExpectedGuidLength}");
                Assert.That(span, Is.SameAs(activeSpan), "The span we created should be the active span");
            });
            Assert.Multiple(() =>
            {
                Assert.That(span.GetOperationName(), Is.EqualTo(name), $"span operation name should be {name} but got {span.GetOperationName()} instead");
                Assert.That(span.GetDurationInSeconds(), Is.GreaterThan(0.0), $"span duration must be greater than 0 but got {span.GetDurationInSeconds()} instead");
                Assert.That(string.IsNullOrEmpty(span.Guid()), Is.False, "span guid must not be null or empty");
            });
        }

        [Test]
        public void BuildRootSpanWith_Start()
        {
            var tracer = LambdaTracer.Instance;
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name);
            LambdaRootSpan span1 = (LambdaRootSpan)spanBuilder.Start();
            LambdaRootSpan span2 = (LambdaRootSpan)spanBuilder.Start();

            Assert.That(tracer.ActiveSpan, Is.Null);
            Thread.Sleep(500);

            span1.Finish();
            span2.Finish();

            Assert.That(span1, Is.Not.Null, "span1 must not be null");
            Assert.Multiple(() =>
            {
                Assert.That(span1.Guid(), Has.Length.EqualTo(ExpectedGuidLength), $"span1 guid length must be {ExpectedGuidLength}");
                Assert.That(span1.GetOperationName(), Is.EqualTo(name), $"span1 operation name should be {name} but got {span1.GetOperationName()} instead");
                Assert.That(span1.GetDurationInSeconds(), Is.GreaterThan(0.0), $"span1 duration must be greater than 0 but got {span1.GetDurationInSeconds()} instead");
                Assert.That(string.IsNullOrEmpty(span1.Guid()), Is.False, "span1 guid must not be null or empty");

                Assert.That(span2, Is.Not.Null, "span2 must not be null");
            });
            Assert.Multiple(() =>
            {
                Assert.That(span2.Guid(), Has.Length.EqualTo(ExpectedGuidLength), $"span2 guid length must be {ExpectedGuidLength}");
                Assert.That(span2.GetOperationName(), Is.EqualTo(name), $"span2 operation name should be {name} but got {span2.GetOperationName()} instead");
                Assert.That(span2.GetDurationInSeconds(), Is.GreaterThan(0.0), $"span2 duration must be greater than 0 but got {span2.GetDurationInSeconds()} instead");
                Assert.That(string.IsNullOrEmpty(span2.Guid()), Is.False, "span2 guid must not be null or empty");

                Assert.That(span2.Guid(), Is.Not.EqualTo(span1.Guid()), "span1 and span2 must have different guids");
            });
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

            Assert.That(childSpan.RootSpan.Guid(), Is.EqualTo(rootSpan.Guid()), "The guid of the child span's RootSpan should match the guid of the root span");
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

            Assert.Multiple(() =>
            {
                Assert.That(span.Intrinsics.ContainsKey("traceId"), Is.True);
                Assert.That(span.Intrinsics["traceId"].IsEqualTo(_dtPayload.TraceId));
                Assert.That(span.Intrinsics["traceId"].IsNotEqualTo(span.TransactionState.TransactionId));
            });
        }

        [Test]
        public void TracerId_DoesMatch_TransactionId_WithoutDistributedTracePayload()
        {
            var tracer = LambdaTracer.Instance;
            var name = "operationName";
            var spanBuilder = tracer.BuildSpan(name);
            var span = (LambdaRootSpan)spanBuilder.Start();
            span.Finish();

            Assert.Multiple(() =>
            {
                Assert.That(span.Intrinsics.ContainsKey("traceId"), Is.True);
                Assert.That(span.Intrinsics["traceId"].IsNotEqualTo(_dtPayload.TraceId));
                Assert.That(span.Intrinsics["traceId"].IsEqualTo(span.TransactionState.TransactionId));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(span.Intrinsics.ContainsKey("priority"), Is.True);
                Assert.That(span.Intrinsics["priority"], Is.EqualTo(_dtPayload.Priority));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(span.Intrinsics.ContainsKey("priority"), Is.True);
                Assert.That(span.Intrinsics["priority"], Is.GreaterThanOrEqualTo(0));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(span.Intrinsics.ContainsKey("sampled"), Is.True);
                Assert.That(span.Intrinsics["sampled"], Is.True);
            });
        }
    }
}
