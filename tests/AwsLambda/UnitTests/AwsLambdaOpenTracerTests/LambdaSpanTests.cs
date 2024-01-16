// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Threading;

namespace NewRelic.Tests.AwsLambda.AwsLambdaOpenTracerTests
{
    [TestFixture]
    public class LambdaSpanTests
    {
        [Test]
        public void RootSpanDurationTest()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            Thread.Sleep(500);
            rootSpan.Finish();
            var endTime = DateTimeOffset.UtcNow;
            var maxDuration = (endTime - startTime).TotalSeconds;
            var spanDuration = rootSpan.GetDurationInSeconds();
            ClassicAssert.IsTrue(spanDuration > 0, $"Span duration ({spanDuration}) must be greater than 0");
            ClassicAssert.IsTrue(spanDuration <= maxDuration, $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
        }

        [Test]
        public void ChildSpanDurationTest()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
            Thread.Sleep(500);
            childSpan.Finish();
            var endTime = DateTimeOffset.UtcNow;
            var maxDuration = (endTime - startTime).TotalSeconds;
            var spanDuration = childSpan.GetDurationInSeconds();
            ClassicAssert.IsTrue(spanDuration > 0, $"Span duration ({spanDuration}) must be greater than 0");
            ClassicAssert.IsTrue(spanDuration <= maxDuration, $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
        }

        [Test]
        public void RootSpan_Set_Get_Tags()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            rootSpan.SetTag("key1", "stringValue");
            rootSpan.SetTag("key2", true);
            rootSpan.SetTag("key3", 1.0);
            rootSpan.SetTag("key4", 1);
            rootSpan.SetTag(new global::OpenTracing.Tag.BooleanTag("BooleanTagKey"), true);
            rootSpan.SetTag(new global::OpenTracing.Tag.IntOrStringTag("IntOrStringTagKey"), "stringValue");
            rootSpan.SetTag(new global::OpenTracing.Tag.IntTag("IntTagKey"), 1);
            rootSpan.SetTag(new global::OpenTracing.Tag.StringTag("StringTagKey"), "stringValue");

            ClassicAssert.AreEqual(rootSpan.GetTag("key1"), "stringValue");
            ClassicAssert.AreEqual(rootSpan.GetTag("key2"), true);
            ClassicAssert.AreEqual(rootSpan.GetTag("key3"), 1.0);
            ClassicAssert.AreEqual(rootSpan.GetTag("key4"), 1);
            ClassicAssert.AreEqual(rootSpan.GetTag("BooleanTagKey"), true);
            ClassicAssert.AreEqual(rootSpan.GetTag("IntOrStringTagKey"), "stringValue");
            ClassicAssert.AreEqual(rootSpan.GetTag("IntTagKey"), 1);
            ClassicAssert.AreEqual(rootSpan.GetTag("StringTagKey"), "stringValue");
        }
        [Test]
        public void ChildSpan_Set_Get_Tags()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
            childSpan.SetTag("key1", "stringValue");
            childSpan.SetTag("key2", true);
            childSpan.SetTag("key3", 1.0);
            childSpan.SetTag("key4", 1);
            childSpan.SetTag(new global::OpenTracing.Tag.BooleanTag("BooleanTagKey"), true);
            childSpan.SetTag(new global::OpenTracing.Tag.IntOrStringTag("IntOrStringTagKey"), "stringValue");
            childSpan.SetTag(new global::OpenTracing.Tag.IntTag("IntTagKey"), 1);
            childSpan.SetTag(new global::OpenTracing.Tag.StringTag("StringTagKey"), "stringValue");

            ClassicAssert.AreEqual(childSpan.GetTag("key1"), "stringValue");
            ClassicAssert.AreEqual(childSpan.GetTag("key2"), true);
            ClassicAssert.AreEqual(childSpan.GetTag("key3"), 1.0);
            ClassicAssert.AreEqual(childSpan.GetTag("key4"), 1);
            ClassicAssert.AreEqual(childSpan.GetTag("BooleanTagKey"), true);
            ClassicAssert.AreEqual(childSpan.GetTag("IntOrStringTagKey"), "stringValue");
            ClassicAssert.AreEqual(childSpan.GetTag("IntTagKey"), 1);
            ClassicAssert.AreEqual(childSpan.GetTag("StringTagKey"), "stringValue");
        }

        [Test]
        public void SpanCategory()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
            childSpan.SetTag("http.status_code", "404");
            childSpan.SetTag("span.kind", "client");
            childSpan.Finish();
            rootSpan.SetTag("http.status_code", "404");
            rootSpan.SetTag("span.kind", "client");
            rootSpan.Finish();

            ClassicAssert.AreEqual("http", childSpan.Intrinsics["category"]);
            ClassicAssert.AreEqual("http", rootSpan.Intrinsics["category"]);
        }

        [Test]
        public void SpanParenting()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
            var grandChildSpan = TestUtil.CreateSpan("grandChildOperation", startTime, new Dictionary<string, object>(), childSpan, "grandchildguid");

            ClassicAssert.IsFalse(rootSpan.Intrinsics.ContainsKey("parentId"));
            ClassicAssert.AreEqual("rootguid", childSpan.Intrinsics["parentId"]);
            ClassicAssert.AreEqual("childguid", grandChildSpan.Intrinsics["parentId"]);
        }

        [Test]
        public void SpanHasCorrectAttributes()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = (LambdaRootSpan)TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");

            rootSpan.SetTag("http.status_code", 200);
            childSpan.SetTag("http.status_code", 500);

            childSpan.Finish();
            rootSpan.Finish();

            ClassicAssert.AreEqual(rootSpan.Intrinsics["type"], "Span");
            ClassicAssert.AreEqual(rootSpan.Intrinsics["name"], "rootOperation");
            ClassicAssert.NotNull(rootSpan.Intrinsics["duration"]);
            ClassicAssert.NotNull(rootSpan.Intrinsics["timestamp"]);
            ClassicAssert.AreEqual(rootSpan.Intrinsics["category"], "http");
            ClassicAssert.AreEqual(rootSpan.Intrinsics["nr.entryPoint"], true);
            ClassicAssert.IsFalse(rootSpan.Intrinsics.ContainsKey("parentId"));
            ClassicAssert.AreEqual(rootSpan.Intrinsics["transactionId"], rootSpan.TransactionState.TransactionId);

            ClassicAssert.AreEqual(rootSpan.Intrinsics["span.kind"], "client");
            ClassicAssert.IsFalse(rootSpan.UserAttributes.ContainsKey("http.status_code"));

            ClassicAssert.AreEqual(rootSpan.AgentAttributes["response.status"], "200");
            ClassicAssert.AreEqual(rootSpan.AgentAttributes["http.statusCode"], 200);

            ClassicAssert.AreEqual(childSpan.Intrinsics["type"], "Span");
            ClassicAssert.AreEqual(childSpan.Intrinsics["name"], "childOperation");
            ClassicAssert.NotNull(childSpan.Intrinsics["duration"]);
            ClassicAssert.NotNull(childSpan.Intrinsics["timestamp"]);
            ClassicAssert.AreEqual(childSpan.Intrinsics["category"], "http");
            ClassicAssert.IsFalse(childSpan.Intrinsics.ContainsKey("nr.entryPoint"));
            ClassicAssert.AreEqual(childSpan.Intrinsics["parentId"], "rootguid");
            ClassicAssert.AreEqual(childSpan.Intrinsics["transactionId"], childSpan.RootSpan.TransactionState.TransactionId);

            ClassicAssert.AreEqual(childSpan.Intrinsics["span.kind"], "client");
            ClassicAssert.IsFalse(childSpan.UserAttributes.ContainsKey("http.status_code"));

            ClassicAssert.AreEqual(childSpan.AgentAttributes["response.status"], "500");
            ClassicAssert.AreEqual(childSpan.AgentAttributes["http.statusCode"], 500);
        }
    }
}
