// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.CrossApplicationTracing;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class ExternalSegmentDataTests
    {
        #region IsCombinableWith

        private static Segment createExternalSegmentBuilder(TimeSpan relativeStart, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, Uri uri, string method, CrossApplicationResponseData crossApplicationResponseData, bool combinable)
        {
            var data = new ExternalSegmentData(uri, method, crossApplicationResponseData);
            var segment = new Segment(SimpleSegmentDataTestHelpers.CreateTransactionSegmentState(uniqueId, parentId), methodCallData);
            segment.SetSegmentData(data);
            segment.Combinable = combinable;

            return segment;
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.True);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentUri()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.newrelic.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDeclaredMethod()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod2", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createExternalSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), new Uri("http://www.google.com"), "declaredMethod", new CrossApplicationResponseData("cpId", "name", 1.1f, 2.2f, 3), true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
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
            Assert.That(segmentData, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(newSegment.RelativeStartTime, Is.EqualTo(newStartTime)),
                () => Assert.That(newSegment.Duration, Is.EqualTo(newDuration)),
                () => Assert.That(newSegment.MethodCallData.TypeName, Is.EqualTo("type")),
                () => Assert.That(newSegment.MethodCallData.MethodName, Is.EqualTo("method")),
                () => Assert.That(newSegment.MethodCallData.InvocationTargetHashCode, Is.EqualTo(1)),
                () => Assert.That(segmentData.Uri, Is.EqualTo(new Uri("http://www.google.com"))),
                () => Assert.That(segmentData.Method, Is.EqualTo("declaredMethod")),
                () => Assert.That(newSegment.Parameters.Count(), Is.EqualTo(2)),
                () => Assert.That(newSegment.Parameters.ToDictionary()["foo"], Is.EqualTo("bar")),
                () => Assert.That(newSegment.Parameters.ToDictionary()["zip"], Is.EqualTo("zap")),
                () => Assert.That(newSegment.Combinable, Is.EqualTo(true))
                );
        }

        #endregion CreateSimilar
    }
}
