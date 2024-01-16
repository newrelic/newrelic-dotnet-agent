// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Segments.Tests
{
    [TestFixture]
    public class MessageBrokerSegmentDataTests
    {
        #region IsCombinableWith

        public static Segment createMessageBrokerSegmentBuilder(TimeSpan start, TimeSpan duration, int uniqueId, int? parentId, MethodCallData methodCallData, IEnumerable<KeyValuePair<string, object>> enumerable, string vendor, string queue, MetricNames.MessageBrokerDestinationType type, MetricNames.MessageBrokerAction action, bool combinable)
        {
            var segment = new Segment(SimpleSegmentDataTests.createTransactionSegmentState(uniqueId, parentId), methodCallData);
            segment.SetSegmentData(new MessageBrokerSegmentData(vendor, queue, type, action));
            segment.Combinable = combinable;

            return new Segment(start, duration, segment, null);
        }

        [Test]
        public void IsCombinableWith_ReturnsTrue_ForIdenticalSegments()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsTrue(segment1.IsCombinableWith(segment2));
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentVendor()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor2", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDestination()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueB", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDestinationType()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Topic, MetricNames.MessageBrokerAction.Consume, true);

            ClassicAssert.IsFalse(segment1.IsCombinableWith(segment2));
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentAction()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Produce, true);

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
            var oldSegment = createMessageBrokerSegmentBuilder(new TimeSpan(), oldDuration, 2, 1, new MethodCallData("type", "method", 1), oldParameters, "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            var newStartTime = TimeSpan.FromSeconds(5);
            var newDuration = TimeSpan.FromSeconds(5);
            var newParameters = new Dictionary<string, object> { { "foo", "bar" }, { "zip", "zap" } };
            var newSegment = oldSegment.CreateSimilar(newStartTime, newDuration, newParameters);

            var segmentData = newSegment.Data as MessageBrokerSegmentData;
            ClassicAssert.NotNull(segmentData);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(newStartTime, newSegment.RelativeStartTime),
                () => ClassicAssert.AreEqual(newDuration, newSegment.Duration),
                () => ClassicAssert.AreEqual("type", newSegment.MethodCallData.TypeName),
                () => ClassicAssert.AreEqual("method", newSegment.MethodCallData.MethodName),
                () => ClassicAssert.AreEqual(1, newSegment.MethodCallData.InvocationTargetHashCode),
                () => ClassicAssert.AreEqual(MetricNames.MessageBrokerDestinationType.Queue, segmentData.DestinationType),
                () => ClassicAssert.AreEqual(MetricNames.MessageBrokerAction.Consume, segmentData.Action),
                () => ClassicAssert.AreEqual("vendor1", segmentData.Vendor),
                () => ClassicAssert.AreEqual("queueA", segmentData.Destination),
                () => ClassicAssert.AreEqual(2, newSegment.Parameters.Count()),
                () => ClassicAssert.AreEqual("bar", newSegment.Parameters.ToDictionary()["foo"]),
                () => ClassicAssert.AreEqual("zap", newSegment.Parameters.ToDictionary()["zip"]),
                () => ClassicAssert.AreEqual(true, newSegment.Combinable)
                );
        }

        #endregion CreateSimilar

    }
}
