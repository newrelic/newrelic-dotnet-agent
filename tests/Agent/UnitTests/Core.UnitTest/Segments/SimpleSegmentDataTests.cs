// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using Telerik.JustMock;
using NewRelic.Agent.Core.Transactions;
using NewRelic.Agent.Core.Attributes;
using NewRelic.Agent.Core.Spans;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class SimpleSegmentDataTests
    {
        #region IsCombinableWith

        public static ITransactionSegmentState createTransactionSegmentState(int uniqueId, int? parentId, int managedThreadId = 1)
        {
            var segmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => segmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));
            Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
            Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
            Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
            return segmentState;
        }

        public static Segment createSimpleSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, string name, bool combinable, int managedThreadId = 1)
        {
            var segmentState = createTransactionSegmentState(uniqueId, parentId, managedThreadId);
            var segment = new Segment(segmentState, methodCallData);
            segment.SetSegmentData(new SimpleSegmentData(name));
            segment.Combinable = combinable;

            return segment.CreateSimilar(start, duration, parameters);
        }

        [Test]
        public void ThreadIdIsSet()
        {
            var segment = new Segment(createTransactionSegmentState(3, null, 666), new MethodCallData("type", "method", 1));
            segment.SetSegmentData(new SimpleSegmentData("test"));

            ClassicAssert.AreEqual(666, segment.ThreadId);
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = new SimpleSegmentData("name");
            var segment2 = new SimpleSegmentData("name");

            ClassicAssert.IsTrue(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void ExclusiveDuration_Synchronous()
        {
            var parent = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 0, null, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true, 666);
            var child = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(1), 1, 0, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true, 666);
            parent.ChildFinished(child);

            ClassicAssert.AreEqual(1, parent.ExclusiveDurationOrZero.TotalSeconds);
            ClassicAssert.AreEqual(1, child.ExclusiveDurationOrZero.TotalSeconds);

            // second call should be ignored
            parent.ChildFinished(child);
            ClassicAssert.AreEqual(1, parent.ExclusiveDurationOrZero.TotalSeconds);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name2", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "type", "method", true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        #endregion IsCombinableWith

        #region CreateSimilar

        [Test]
        public void CreateSimilar_ReturnsCorrectValues()
        {
            var oldStartTime = DateTime.Now;
            var oldDuration = TimeSpan.FromSeconds(2);
            var oldParameters = new Dictionary<string, object> { { "flim", "flam" } };
            var oldSegment = createSimpleSegmentBuilder(new TimeSpan(), oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, "name", true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<string, object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var segmentData = newSegment.Data as SimpleSegmentData;
            ClassicAssert.NotNull(segmentData);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(newStartTime, newSegment.RelativeStartTime),
                () => ClassicAssert.AreEqual(newDuration, newSegment.Duration),
                () => ClassicAssert.AreEqual("type", newSegment.MethodCallData.TypeName),
                () => ClassicAssert.AreEqual("method", newSegment.MethodCallData.MethodName),
                () => ClassicAssert.AreEqual(1, newSegment.MethodCallData.InvocationTargetHashCode),
                () => ClassicAssert.AreEqual("name", segmentData.Name),
                () => ClassicAssert.AreEqual(2, newSegment.Parameters.Count()),
                () => ClassicAssert.AreEqual("bar", newSegment.Parameters.ToDictionary()["foo"]),
                () => ClassicAssert.AreEqual("zap", newSegment.Parameters.ToDictionary()["zip"]),
                () => ClassicAssert.AreEqual(true, newSegment.Combinable)
                );
        }

        #endregion CreateSimilar
    }
}
