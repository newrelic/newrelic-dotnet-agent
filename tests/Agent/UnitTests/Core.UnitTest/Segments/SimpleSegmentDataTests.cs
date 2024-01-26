// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
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

        [Test]
        public void ThreadIdIsSet()
        {
            var segment = new Segment(SimpleSegmentDataTestHelpers.CreateTransactionSegmentState(3, null, 666), new MethodCallData("type", "method", 1));
            segment.SetSegmentData(new SimpleSegmentData("test"));

            Assert.That(segment.ThreadId, Is.EqualTo(666));
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = new SimpleSegmentData("name");
            var segment2 = new SimpleSegmentData("name");

            Assert.That(segment1.IsCombinableWith(segment2), Is.True);
        }

        [Test]
        public void ExclusiveDuration_Synchronous()
        {
            var parent = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 0, null, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true, 666);
            var child = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(1), 1, 0, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true, 666);
            parent.ChildFinished(child);

            Assert.Multiple(() =>
            {
                Assert.That(parent.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(1));
                Assert.That(child.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(1));
            });

            // second call should be ignored
            parent.ChildFinished(child);
            Assert.That(parent.ExclusiveDurationOrZero.TotalSeconds, Is.EqualTo(1));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", false);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentName()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name2", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentSegmentType()
        {
            var segment1 = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "name", true);
            var segment2 = MethodSegmentDataTestHelpers.CreateMethodSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "type", "method", true);

            Assert.That(segment1.IsCombinableWith(segment2), Is.False);
        }

        #endregion IsCombinableWith

        #region CreateSimilar

        [Test]
        public void CreateSimilar_ReturnsCorrectValues()
        {
            var oldStartTime = DateTime.Now;
            var oldDuration = TimeSpan.FromSeconds(2);
            var oldParameters = new Dictionary<string, object> { { "flim", "flam" } };
            var oldSegment = SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(new TimeSpan(), oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, "name", true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<string, object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var segmentData = newSegment.Data as SimpleSegmentData;
            Assert.That(segmentData, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(newSegment.RelativeStartTime, Is.EqualTo(newStartTime)),
                () => Assert.That(newSegment.Duration, Is.EqualTo(newDuration)),
                () => Assert.That(newSegment.MethodCallData.TypeName, Is.EqualTo("type")),
                () => Assert.That(newSegment.MethodCallData.MethodName, Is.EqualTo("method")),
                () => Assert.That(newSegment.MethodCallData.InvocationTargetHashCode, Is.EqualTo(1)),
                () => Assert.That(segmentData.Name, Is.EqualTo("name")),
                () => Assert.That(newSegment.Parameters.Count(), Is.EqualTo(2)),
                () => Assert.That(newSegment.Parameters.ToDictionary()["foo"], Is.EqualTo("bar")),
                () => Assert.That(newSegment.Parameters.ToDictionary()["zip"], Is.EqualTo("zap")),
                () => Assert.That(newSegment.Combinable, Is.EqualTo(true))
                );
        }

        #endregion CreateSimilar
    }

    public static class SimpleSegmentDataTestHelpers
    {
        public static ITransactionSegmentState CreateTransactionSegmentState(int uniqueId, int? parentId, int managedThreadId = 1)
        {
            var segmentState = Mock.Create<ITransactionSegmentState>();
            Mock.Arrange(() => segmentState.AttribDefs).Returns(() => new AttributeDefinitions(new AttributeFilter(new AttributeFilter.Settings())));
            Mock.Arrange(() => segmentState.ParentSegmentId()).Returns(parentId);
            Mock.Arrange(() => segmentState.CallStackPush(Arg.IsAny<Segment>())).Returns(uniqueId);
            Mock.Arrange(() => segmentState.CurrentManagedThreadId).Returns(managedThreadId);
            return segmentState;
        }

        public static Segment CreateSimpleSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> parameters, string name, bool combinable, int managedThreadId = 1)
        {
            var segmentState = CreateTransactionSegmentState(uniqueId, parentId, managedThreadId);
            var segment = new Segment(segmentState, methodCallData);
            segment.SetSegmentData(new SimpleSegmentData(name));
            segment.Combinable = combinable;

            return segment.CreateSimilar(start, duration, parameters);
        }
    }
}
