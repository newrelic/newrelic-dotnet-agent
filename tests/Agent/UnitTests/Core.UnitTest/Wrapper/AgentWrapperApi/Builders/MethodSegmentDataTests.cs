using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using NewRelic.SystemExtensions.Collections.Generic;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
	[TestFixture]
	public class MethodSegmentDataTests
	{
		#region IsCombinableWith

		public static TypedSegment<MethodSegmentData> createMethodSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> enumerable, string type, string method, bool combinable)
		{
			return new TypedSegment<MethodSegmentData>(start, duration,
				new TypedSegment<MethodSegmentData>(SimpleSegmentDataTests.createTransactionSegmentState(uniqueId, parentId), methodCallData, new MethodSegmentData(type, method), combinable));
		}

		[Test]
		public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);

			Assert.IsTrue(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", false);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", false);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", false);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentDeclaredType()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType2", "declaredMethod", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentDeclaredMethod()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod2", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		[Test]
		public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
		{
			var segment1 = createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "declaredType", "declaredMethod", true);
			var segment2 = SimpleSegmentDataTests.createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

			Assert.IsFalse(segment1.IsCombinableWith(segment2));
		}

		#endregion IsCombinableWith

		#region CreateSimilar

		[Test]
		public void CreateSimilar_ReturnsCorrectValues()
		{
			var oldStartTime = DateTime.Now;
			var oldDuration = TimeSpan.FromSeconds(2);
			var oldParameters = new Dictionary<String, Object> {{"flim", "flam"}};
			var oldSegment = createMethodSegmentBuilder(new TimeSpan(), oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, "declaredType", "declaredMethod", true);

			var newStartTime = TimeSpan.FromSeconds(5);
			var newDuration = TimeSpan.FromSeconds(5);
			var newParameters = new Dictionary<String, Object> {{"foo", "bar"}, {"zip", "zap"}};
			var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

			var newTypedSegment = newSegment as TypedSegment<MethodSegmentData>;
			Assert.NotNull(newTypedSegment);

			NrAssert.Multiple(
				() => Assert.AreEqual(newStartTime, newTypedSegment.RelativeStartTime),
				() => Assert.AreEqual(newDuration, newTypedSegment.Duration),
				() => Assert.AreEqual("type", newTypedSegment.MethodCallData.TypeName),
				() => Assert.AreEqual("method", newTypedSegment.MethodCallData.MethodName),
				() => Assert.AreEqual(1, newTypedSegment.MethodCallData.InvocationTargetHashCode),
				() => Assert.AreEqual("declaredType", newTypedSegment.TypedData.Type),
				() => Assert.AreEqual("declaredMethod", newTypedSegment.TypedData.Method),
				() => Assert.AreEqual(2, newTypedSegment.Parameters.Count()),
				() => Assert.AreEqual("bar", newTypedSegment.Parameters.ToDictionary()["foo"]),
				() => Assert.AreEqual("zap", newTypedSegment.Parameters.ToDictionary()["zip"]),
				() => Assert.AreEqual(true, newTypedSegment.Combinable)
				);
		}

		#endregion CreateSimilar
	}
}
