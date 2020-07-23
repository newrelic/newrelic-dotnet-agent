using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{
    [TestFixture]
    public class SimpleSegmentDataTests
    {
        #region IsCombinableWith

        public static ITransactionSegmentState createTransactionSegmentState(int uniqueId, int? parentId, int managedThreadId = 1)
        {
            var segmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
            Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
            Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
            return segmentState;
        }

        public static TypedSegment<SimpleSegmentData> createSimpleSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, string name, bool combinable, int managedThreadId = 1)
        {
            var segmentState = createTransactionSegmentState(uniqueId, parentId, managedThreadId);

            return (TypedSegment<SimpleSegmentData>)new TypedSegment<SimpleSegmentData>(segmentState, methodCallData, new SimpleSegmentData(name), combinable)
                .CreateSimilar(start, duration, parameters);
        }

        [Test]
        public void ThreadIdIsSet()
        {
            var segment = new TypedSegment<SimpleSegmentData>(createTransactionSegmentState(3, null, 666), new MethodCallData("type", "method", 1), new SimpleSegmentData("test"), false);
            Assert.AreEqual(666, segment.ThreadId);
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = new SimpleSegmentData("name");
            var segment2 = new SimpleSegmentData("name");

            Assert.IsTrue(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void ExclusiveDuration_Synchronous()
        {
            var parent = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 0, null, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true, 666);
            var child = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(1), 1, 0, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true, 666);
            parent.ChildFinished(child);

            Assert.AreEqual(1, parent.ExclusiveDurationOrZero.TotalSeconds);
            Assert.AreEqual(1, child.ExclusiveDurationOrZero.TotalSeconds);

            // second call should be ignored
            parent.ChildFinished(child);
            Assert.AreEqual(1, parent.ExclusiveDurationOrZero.TotalSeconds);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentName()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name2", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = createSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "name", true);
            var segment2 = MethodSegmentDataTests.createMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<String, Object>>(), "type", "method", true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        #endregion IsCombinableWith

        #region CreateSimilar

        [Test]
        public void CreateSimilar_ReturnsCorrectValues()
        {
            var oldStartTime = DateTime.Now;
            var oldDuration = TimeSpan.FromSeconds(2);
            var oldParameters = new Dictionary<String, Object> { { "flim", "flam" } };
            var oldSegment = createSimpleSegmentBuilder(new TimeSpan(), oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, "name", true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<String, Object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var newTypedSegment = newSegment as TypedSegment<SimpleSegmentData>;
            Assert.NotNull(newTypedSegment);

            NrAssert.Multiple(
                () => Assert.AreEqual(newStartTime, newTypedSegment.RelativeStartTime),
                () => Assert.AreEqual(newDuration, newTypedSegment.Duration),
                () => Assert.AreEqual("type", newTypedSegment.MethodCallData.TypeName),
                () => Assert.AreEqual("method", newTypedSegment.MethodCallData.MethodName),
                () => Assert.AreEqual(1, newTypedSegment.MethodCallData.InvocationTargetHashCode),
                () => Assert.AreEqual("name", newTypedSegment.TypedData.Name),
                () => Assert.AreEqual(2, newTypedSegment.Parameters.Count()),
                () => Assert.AreEqual("bar", newTypedSegment.Parameters.ToDictionary()["foo"]),
                () => Assert.AreEqual("zap", newTypedSegment.Parameters.ToDictionary()["zip"]),
                () => Assert.AreEqual(true, newTypedSegment.Combinable)
                );
        }

        #endregion CreateSimilar
    }
}
