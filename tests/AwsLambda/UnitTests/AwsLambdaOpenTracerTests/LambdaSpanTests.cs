// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using System.Collections.Generic;
using System;
using System.Threading;
using NewRelic.OpenTracing.AmazonLambda;

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
            Assert.Multiple(() =>
            {
                Assert.That(spanDuration, Is.GreaterThan(0), $"Span duration ({spanDuration}) must be greater than 0");
                Assert.That(spanDuration, Is.LessThanOrEqualTo(maxDuration), $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
            });
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
            Assert.Multiple(() =>
            {
                Assert.That(spanDuration, Is.GreaterThan(0), $"Span duration ({spanDuration}) must be greater than 0");
                Assert.That(spanDuration, Is.LessThanOrEqualTo(maxDuration), $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(rootSpan.GetTag("key1"), Is.EqualTo("stringValue"));
                Assert.That(rootSpan.GetTag("key2"), Is.EqualTo(true));
                Assert.That(rootSpan.GetTag("key3"), Is.EqualTo(1.0));
                Assert.That(rootSpan.GetTag("key4"), Is.EqualTo(1));
                Assert.That(rootSpan.GetTag("BooleanTagKey"), Is.EqualTo(true));
                Assert.That(rootSpan.GetTag("IntOrStringTagKey"), Is.EqualTo("stringValue"));
                Assert.That(rootSpan.GetTag("IntTagKey"), Is.EqualTo(1));
                Assert.That(rootSpan.GetTag("StringTagKey"), Is.EqualTo("stringValue"));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(childSpan.GetTag("key1"), Is.EqualTo("stringValue"));
                Assert.That(childSpan.GetTag("key2"), Is.EqualTo(true));
                Assert.That(childSpan.GetTag("key3"), Is.EqualTo(1.0));
                Assert.That(childSpan.GetTag("key4"), Is.EqualTo(1));
                Assert.That(childSpan.GetTag("BooleanTagKey"), Is.EqualTo(true));
                Assert.That(childSpan.GetTag("IntOrStringTagKey"), Is.EqualTo("stringValue"));
                Assert.That(childSpan.GetTag("IntTagKey"), Is.EqualTo(1));
                Assert.That(childSpan.GetTag("StringTagKey"), Is.EqualTo("stringValue"));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(childSpan.Intrinsics["category"], Is.EqualTo("http"));
                Assert.That(rootSpan.Intrinsics["category"], Is.EqualTo("http"));
            });
        }

        [Test]
        public void SpanParenting()
        {
            var startTime = DateTimeOffset.UtcNow;
            var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
            var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
            var grandChildSpan = TestUtil.CreateSpan("grandChildOperation", startTime, new Dictionary<string, object>(), childSpan, "grandchildguid");

            Assert.Multiple(() =>
            {
                Assert.That(rootSpan.Intrinsics.ContainsKey("parentId"), Is.False);
                Assert.That(childSpan.Intrinsics["parentId"], Is.EqualTo("rootguid"));
                Assert.That(grandChildSpan.Intrinsics["parentId"], Is.EqualTo("childguid"));
            });
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

            Assert.Multiple(() =>
            {
                Assert.That(rootSpan.Intrinsics["type"], Is.EqualTo("Span"));
                Assert.That(rootSpan.Intrinsics["name"], Is.EqualTo("rootOperation"));
                Assert.That(rootSpan.Intrinsics["duration"], Is.Not.Null);
                Assert.That(rootSpan.Intrinsics["timestamp"], Is.Not.Null);
                Assert.That(rootSpan.Intrinsics["category"], Is.EqualTo("http"));
                Assert.That(rootSpan.Intrinsics["nr.entryPoint"], Is.EqualTo(true));
                Assert.That(rootSpan.Intrinsics.ContainsKey("parentId"), Is.False);
                Assert.That(rootSpan.TransactionState.TransactionId, Is.EqualTo(rootSpan.Intrinsics["transactionId"]));

                Assert.That(rootSpan.Intrinsics["span.kind"], Is.EqualTo("client"));
                Assert.That(rootSpan.UserAttributes.ContainsKey("http.status_code"), Is.False);

                Assert.That(rootSpan.AgentAttributes["response.status"], Is.EqualTo("200"));
                Assert.That(rootSpan.AgentAttributes["http.statusCode"], Is.EqualTo(200));

                Assert.That(childSpan.Intrinsics["type"], Is.EqualTo("Span"));
                Assert.That(childSpan.Intrinsics["name"], Is.EqualTo("childOperation"));
                Assert.That(childSpan.Intrinsics["duration"], Is.Not.Null);
                Assert.That(childSpan.Intrinsics["timestamp"], Is.Not.Null);
                Assert.That(childSpan.Intrinsics["category"], Is.EqualTo("http"));
                Assert.That(childSpan.Intrinsics.ContainsKey("nr.entryPoint"), Is.False);
                Assert.That(childSpan.Intrinsics["parentId"], Is.EqualTo("rootguid"));
                Assert.That(childSpan.RootSpan.TransactionState.TransactionId, Is.EqualTo(childSpan.Intrinsics["transactionId"]));

                Assert.That(childSpan.Intrinsics["span.kind"], Is.EqualTo("client"));
                Assert.That(childSpan.UserAttributes.ContainsKey("http.status_code"), Is.False);

                Assert.That(childSpan.AgentAttributes["response.status"], Is.EqualTo("500"));
                Assert.That(childSpan.AgentAttributes["http.statusCode"], Is.EqualTo(500));
            });
        }
    }
}
