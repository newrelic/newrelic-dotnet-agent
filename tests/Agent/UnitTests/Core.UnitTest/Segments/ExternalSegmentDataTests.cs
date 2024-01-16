// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class ExternalSegmentDataTests
    {
        #region IsCombinableWith

        public static Segment createExternalSegmentBuilder(TimeSpan relativeStart, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, Uri uri, string method, CrossApplicationResponseData crossApplicationResponseData, bool combinable)
        {
            var data = new ExternalSegmentData(uri, method, crossApplicationResponseData);
            var segment = new Segment(SimpleSegmentDataTests.createTransactionSegmentState(uniqueId, parentId), methodCallData);
            segment.SetSegmentData(data);
            segment.Combinable = combinable;

            return segment;
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsTrue(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentUri()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.newrelic.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDeclaredMethod()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod2", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = SimpleSegmentDataTests.createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        #endregion IsCombinableWith

        #region CreateSimilar

        [Test]
        public void CreateSimilar_ReturnsCorrectValues()
        {
            var oldStartTime = new TimeSpan();
            var oldDuration = TimeSpan.FromSeconds(2);
            var oldParameters = new Dictionary<string, object> { { "flim", "flam" } };
            var oldSegment = createExternalSegmentBuilder(oldStartTime, oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<string, object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var segmentData = newSegment.Data as ExternalSegmentData;
            ClassicAssert.NotNull(segmentData);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(newStartTime, newSegment.RelativeStartTime),
                () => ClassicAssert.AreEqual(newDuration, newSegment.Duration),
                () => ClassicAssert.AreEqual("type", newSegment.MethodCallData.TypeName),
                () => ClassicAssert.AreEqual("method", newSegment.MethodCallData.MethodName),
                () => ClassicAssert.AreEqual(1, newSegment.MethodCallData.InvocationTargetHashCode),
                () => ClassicAssert.AreEqual(new Uri("http://www.google.com"), segmentData.Uri),
                () => ClassicAssert.AreEqual("declaredMethod", segmentData.Method),
                () => ClassicAssert.AreEqual(2, newSegment.Parameters.Count()),
                () => ClassicAssert.AreEqual("bar", newSegment.Parameters.ToDictionary()["foo"]),
                () => ClassicAssert.AreEqual("zap", newSegment.Parameters.ToDictionary()["zip"]),
                () => ClassicAssert.AreEqual(true, newSegment.Combinable)
                );
        }

        #endregion CreateSimilar
    }
}
