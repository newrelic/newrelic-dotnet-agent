// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Metrics;
using NewRelic.Agent.Core.Spans;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

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

            Assert.IsTrue(segment1.IsCombinableWith(segment2));
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentCombinable()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfBothNotCombinable()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, false);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentHashCode()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 2), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentTypeName()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type2", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentMethodName()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method2", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentVendor()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor2", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDestination()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueB", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }

        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentDestinationType()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Topic, MetricNames.MessageBrokerAction.Consume, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
        }


        [Test]
        public void IsCombinableWith_ReturnsFalse_IfDifferentAction()
        {
            var segment1 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Consume, true);
            var segment2 = createMessageBrokerSegmentBuilder(new TimeSpan(), TimeSpan.FromSeconds(2), 2, 1, new MethodCallData("type", "method", 1), Enumerable.Empty<KeyValuePair<string, object>>(), "vendor1", "queueA", MetricNames.MessageBrokerDestinationType.Queue, MetricNames.MessageBrokerAction.Produce, true);

            Assert.IsFalse(segment1.IsCombinableWith(segment2));
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
            Assert.NotNull(segmentData);

            NrAssert.Multiple(
                () => Assert.AreEqual(newStartTime, newSegment.RelativeStartTime),
                () => Assert.AreEqual(newDuration, newSegment.Duration),
                () => Assert.AreEqual("type", newSegment.MethodCallData.TypeName),
                () => Assert.AreEqual("method", newSegment.MethodCallData.MethodName),
                () => Assert.AreEqual(1, newSegment.MethodCallData.InvocationTargetHashCode),
                () => Assert.AreEqual(MetricNames.MessageBrokerDestinationType.Queue, segmentData.DestinationType),
                () => Assert.AreEqual(MetricNames.MessageBrokerAction.Consume, segmentData.Action),
                () => Assert.AreEqual("vendor1", segmentData.Vendor),
                () => Assert.AreEqual("queueA", segmentData.Destination),
                () => Assert.AreEqual(2, newSegment.Parameters.Count()),
                () => Assert.AreEqual("bar", newSegment.Parameters.ToDictionary()["foo"]),
                () => Assert.AreEqual("zap", newSegment.Parameters.ToDictionary()["zip"]),
                () => Assert.AreEqual(true, newSegment.Combinable)
                );
        }

        #endregion CreateSimilar

    }
}
