using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class ExternalSegmentDataTests
    {
        #region IsCombinableWith

        public static TypedSegment<ExternalSegmentData> createExternalSegmentBuilder(TimeSpan relativeStart, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, Uri uri, string method, CrossApplicationResponseData crossApplicationResponseData, bool combinable)
        {
            var data = new ExternalSegmentData(uri, method, crossApplicationResponseData);
            return new TypedSegment<ExternalSegmentData>(SimpleSegmentDataTests.createTransactionSegmentState(uniqueId, parentId), methodCallData, data)
            {
                Combinable = combinable
            };
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsTrue(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentUri()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.newrelic.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDeclaredMethod()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod2", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = SimpleSegmentDataTests.createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        #endregion IsCombinableWith

        #region CreateSimilar

        [Test]
        public void CreateSimilar_ReturnsCorrectValues()
        {
            var oldStartTime = new TimeSpan();
            var oldDuration = TimeSpan.FromSeconds(2);
            var oldParameters = new Dictionary<String, Object> { { "flim", "flam" } };
            var oldSegment = createExternalSegmentBuilder(oldStartTime, oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<String, Object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var newTypedSegment = newSegment as TypedSegment<ExternalSegmentData>;
            Assert.NotNull(newTypedSegment);

            NrAssert.Multiple(
                () => Assert.AreEqual(newStartTime, newTypedSegment.RelativeStartTime),
                () => Assert.AreEqual(newDuration, newTypedSegment.Duration),
                () => Assert.AreEqual("type", newTypedSegment.MethodCallData.TypeName),
                () => Assert.AreEqual("method", newTypedSegment.MethodCallData.MethodName),
                () => Assert.AreEqual(1, newTypedSegment.MethodCallData.InvocationTargetHashCode),
                () => Assert.AreEqual(new Uri("http://www.google.com"), newTypedSegment.TypedData.Uri),
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
