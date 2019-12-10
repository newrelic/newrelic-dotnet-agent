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
			Assert.IsTrue(spanDuration > 0, $"Span duration ({spanDuration}) must be greater than 0");
			Assert.IsTrue(spanDuration <= maxDuration, $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
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
			Assert.IsTrue(spanDuration > 0, $"Span duration ({spanDuration}) must be greater than 0");
			Assert.IsTrue(spanDuration <= maxDuration, $"Span duration ({spanDuration}) must be <= maxDuration ({maxDuration}) ");
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

			Assert.AreEqual(rootSpan.GetTag("key1"), "stringValue");
			Assert.AreEqual(rootSpan.GetTag("key2"), true);
			Assert.AreEqual(rootSpan.GetTag("key3"), 1.0);
			Assert.AreEqual(rootSpan.GetTag("key4"), 1);
			Assert.AreEqual(rootSpan.GetTag("BooleanTagKey"), true);
			Assert.AreEqual(rootSpan.GetTag("IntOrStringTagKey"), "stringValue");
			Assert.AreEqual(rootSpan.GetTag("IntTagKey"), 1);
			Assert.AreEqual(rootSpan.GetTag("StringTagKey"), "stringValue");
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

			Assert.AreEqual(childSpan.GetTag("key1"), "stringValue");
			Assert.AreEqual(childSpan.GetTag("key2"), true);
			Assert.AreEqual(childSpan.GetTag("key3"), 1.0);
			Assert.AreEqual(childSpan.GetTag("key4"), 1);
			Assert.AreEqual(childSpan.GetTag("BooleanTagKey"), true);
			Assert.AreEqual(childSpan.GetTag("IntOrStringTagKey"), "stringValue");
			Assert.AreEqual(childSpan.GetTag("IntTagKey"), 1);
			Assert.AreEqual(childSpan.GetTag("StringTagKey"), "stringValue");
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

			Assert.AreEqual("http", childSpan.Intrinsics["category"]);
			Assert.AreEqual("http", rootSpan.Intrinsics["category"]);
		}

		[Test]
		public void SpanParenting()
		{
			var startTime = DateTimeOffset.UtcNow;
			var rootSpan = TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
			var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");
			var grandChildSpan = TestUtil.CreateSpan("grandChildOperation", startTime, new Dictionary<string, object>(), childSpan, "grandchildguid");

			Assert.IsFalse(rootSpan.Intrinsics.ContainsKey("parentId"));
			Assert.AreEqual("rootguid", childSpan.Intrinsics["parentId"]);
			Assert.AreEqual("childguid", grandChildSpan.Intrinsics["parentId"]);
		}

		[Test]
		public void SpanHasCorrectAttributes()
		{
			var startTime = DateTimeOffset.UtcNow;
			var rootSpan = (LambdaRootSpan)TestUtil.CreateRootSpan("rootOperation", startTime, new Dictionary<string, object>(), "rootguid");
			var childSpan = TestUtil.CreateSpan("childOperation", startTime, new Dictionary<string, object>(), rootSpan, "childguid");

			rootSpan.SetTag("http.status_code", "200");
			childSpan.SetTag("http.status_code", "500");

			childSpan.Finish();
			rootSpan.Finish();

			Assert.AreEqual(rootSpan.Intrinsics["type"], "Span");
			Assert.AreEqual(rootSpan.Intrinsics["name"], "rootOperation");
			Assert.NotNull(rootSpan.Intrinsics["duration"]);
			Assert.NotNull(rootSpan.Intrinsics["timestamp"]);
			Assert.AreEqual(rootSpan.Intrinsics["category"], "http");
			Assert.AreEqual(rootSpan.Intrinsics["nr.entryPoint"], true);
			Assert.IsFalse (rootSpan.Intrinsics.ContainsKey("parentId"));
			Assert.AreEqual(rootSpan.Intrinsics["transactionId"], rootSpan.TransactionState.TransactionId);

			Assert.AreEqual(rootSpan.Intrinsics["span.kind"], "client");
			Assert.IsFalse(rootSpan.UserAttributes.ContainsKey("http.status_code"));

			Assert.AreEqual(rootSpan.AgentAttributes["response.status"], "200");

			Assert.AreEqual(childSpan.Intrinsics["type"], "Span");
			Assert.AreEqual(childSpan.Intrinsics["name"], "childOperation");
			Assert.NotNull(childSpan.Intrinsics["duration"]);
			Assert.NotNull(childSpan.Intrinsics["timestamp"]);
			Assert.AreEqual(childSpan.Intrinsics["category"], "http");
			Assert.IsFalse(childSpan.Intrinsics.ContainsKey("nr.entryPoint"));
			Assert.AreEqual(childSpan.Intrinsics["parentId"], "rootguid");
			Assert.AreEqual(childSpan.Intrinsics["transactionId"], childSpan.RootSpan.TransactionState.TransactionId);

			Assert.AreEqual(childSpan.Intrinsics["span.kind"], "client");
			Assert.IsFalse(childSpan.UserAttributes.ContainsKey("http.status_code"));

			Assert.AreEqual(childSpan.AgentAttributes["response.status"], "500");
		}
	}
}
