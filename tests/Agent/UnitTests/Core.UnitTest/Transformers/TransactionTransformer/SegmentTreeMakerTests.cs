// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer.UnitTest
{
    [TestFixture]
    public class SegmentTreeMakerTests
    {
        private SegmentTreeMaker _segmentTreeMaker;

        [SetUp]
        public void SetUp()
        {
            _segmentTreeMaker = new SegmentTreeMaker();
        }

        [Test]
        public void BuildsEmptyList_IfNoSegments()
        {
            var segments = Enumerable.Empty<Segment>();

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(0, treeRoots.Count());
        }

        [Test]
        public void BuildsSingleNode_IfSingleSegment()
        {
            var uniqueIds = new Dictionary<int, int?>
            {
                {1, null}
            };
            var segments = CreateSegmentListFromUniqueIds(uniqueIds);

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(0, node1.Children.Count());
        }

        [Test]
        public void BuildsTwoRoots_IfTwoUnparentedSegments()
        {
            var uniqueIds = new Dictionary<int, int?>
            {
                {1, null},
                {2, null}
            };
            var segments = CreateSegmentListFromUniqueIds(uniqueIds);

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(2, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            var node2 = treeRoots.ElementAt(1);
            ClassicAssert.NotNull(node1);
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(0, node1.Children.Count());
            ClassicAssert.AreEqual(0, node2.Children.Count());
        }

        [Test]
        public void BuildsNestedNode_IfOneParentedSegment()
        {
            var id1 = 1;
            var id2 = 2;
            var uniqueIds = new Dictionary<int, int?>
            {
                {id1, null},
                {id2, id1}
            };
            var segments = CreateSegmentListFromUniqueIds(uniqueIds);

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(id1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node1.Children.Count());

            var node2 = node1.Children.ElementAt(0);
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(id2, node2.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node2.Children.Count());
        }

        [Test]
        public void BuildsComplicatedTrees_IfSeveralSegments()
        {

            // 1    6
            // 2    7
            // 5 3
            //  4
            var id1 = 1;
            var id2 = 2;
            var id3 = 3;
            var id4 = 4;
            var id5 = 5;
            var id6 = 6;
            var id7 = 7;
            var uniqueIds = new Dictionary<int, int?>
            {
                {id1, null},
                {id2, id1},
                {id3, id2},
                {id4, id3},
                {id5, id2},
                {id6, null},
                {id7, id6}
            };
            var segments = CreateSegmentListFromUniqueIds(uniqueIds);

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(2, treeRoots.Count);

            var node1 = treeRoots.First(node => node.Segment.UniqueId.Equals(1));
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(id1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node1.Children.Count());

            var node2 = node1.Children.First(node => node.Segment.UniqueId.Equals(2));
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(id2, node2.Segment.UniqueId);
            ClassicAssert.AreEqual(2, node2.Children.Count());

            var node3 = node2.Children.First(node => node.Segment.UniqueId.Equals(3));
            ClassicAssert.NotNull(node3);
            ClassicAssert.AreEqual(id3, node3.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node3.Children.Count());

            var node4 = node3.Children.First(node => node.Segment.UniqueId.Equals(4));
            ClassicAssert.NotNull(node4);
            ClassicAssert.AreEqual(id4, node4.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node4.Children.Count());

            var node5 = node2.Children.First(node => node.Segment.UniqueId.Equals(5));
            ClassicAssert.NotNull(node5);
            ClassicAssert.AreEqual(id5, node5.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node5.Children.Count());

            var node6 = treeRoots.First(node => node.Segment.UniqueId.Equals(6));
            ClassicAssert.NotNull(node6);
            ClassicAssert.AreEqual(id6, node6.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node6.Children.Count());

            var node7 = node6.Children.First(node => node.Segment.UniqueId.Equals(7));
            ClassicAssert.NotNull(node7);
            ClassicAssert.AreEqual(id7, node7.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node7.Children.Count());
        }

        [Test]
        public void BuildsComplicatedTrees()
        {
            // This test is a catch-all that should (hopefully) cover every non-error edge case. If this test fails but every other test succeeds then, once the problem is fixed, a new test should be written that explicitly covers that edge case.

            //
            //   1    6
            //   2    7
            //  5 3
            //    4
            //
            var id1 = 1;
            var id2 = 2;
            var id3 = 3;
            var id4 = 4;
            var id5 = 5;
            var id6 = 6;
            var id7 = 7;
            var uniqueIds = new Dictionary<int, int?>
            {
                {id1, null},
                {id2, id1},
                {id3, id2},
                {id4, id3},
                {id5, id2},
                {id6, null},
                {id7, id6}
            };

            var segments = CreateSegmentListFromUniqueIds(uniqueIds);

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(2, treeRoots.Count);

            var node1 = treeRoots.First();
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(id1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node1.Children.Count());

            var node2 = node1.Children.First();
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(id2, node2.Segment.UniqueId);
            ClassicAssert.AreEqual(2, node2.Children.Count());

            var node3 = node2.Children.First();
            ClassicAssert.NotNull(node3);
            ClassicAssert.AreEqual(id3, node3.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node3.Children.Count());

            var node4 = node3.Children.First();
            ClassicAssert.NotNull(node4);
            ClassicAssert.AreEqual(id4, node4.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node4.Children.Count());

            var node5 = node2.Children.ElementAt(1);
            ClassicAssert.NotNull(node5);
            ClassicAssert.AreEqual(id5, node5.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node5.Children.Count());

            var node6 = treeRoots.ElementAt(1);
            ClassicAssert.NotNull(node6);
            ClassicAssert.AreEqual(id6, node6.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node6.Children.Count());

            var node7 = node6.Children.First();
            ClassicAssert.NotNull(node7);
            ClassicAssert.AreEqual(id7, node7.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node7.Children.Count());
        }

        [Test]
        public void CombinesIdenticalAdjacentSiblings()
        {
            var segment1 = CreateSimpleSegment(1, null, "foo");
            var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), true);
            var segment3 = CreateSimpleSegment(3, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), true);
            var segments = new[] { segment1, segment2, segment3 };

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node1.Children.Count());

            var node2 = node1.Children.ElementAt(0);
            ClassicAssert.NotNull(node2);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(0, node2.Children.Count()),

                // The combined segment's start time should be the earliest start time of the original segments
                () => ClassicAssert.AreEqual(1, node2.Segment.RelativeStartTime.TotalSeconds),

                // The combined segment's duration should be the sum of the original segments' durations
                () => ClassicAssert.AreEqual(TimeSpan.FromSeconds(3), node2.Segment.Duration),

                // call_count parameter should be added and should equal the number of nodes that were combined
                () => ClassicAssert.AreEqual(2, node2.Segment.Parameters.ToDictionary()["call_count"])
                );
        }

        [Test]
        public void CombinesIdenticalAdjacentSiblingsRegardlessOfTimeOrder()
        {
            var segment1 = CreateSimpleSegment(1, null, "foo");
            var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1), true);
            var segment3 = CreateSimpleSegment(3, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), true);
            var segments = new[] { segment1, segment2, segment3 };

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(1, node1.Children.Count());

            var node2 = node1.Children.ElementAt(0);
            ClassicAssert.NotNull(node2);

            NrAssert.Multiple(
                () => ClassicAssert.AreEqual(0, node2.Children.Count()),

                // The combined segment's start time should be the earliest start time of the original segments
                () => ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime),

                // The combined segment's duration should be the sum of the original segments' durations
                () => ClassicAssert.AreEqual(TimeSpan.FromSeconds(3), node2.Segment.Duration),

                // call_count parameter should be added and should equal the number of nodes that were combined
                () => ClassicAssert.AreEqual(2, node2.Segment.Parameters.ToDictionary()["call_count"])
                );
        }

        [Test]
        public void DoesNotCombineIdenticalNonAdjacentSiblings()
        {
            var referenceStartTime = DateTime.Now;

            var segment1 = CreateSimpleSegment(1, null, "foo");
            var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), true);
            var segment3 = CreateSimpleSegment(3, 1, "zip", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), true);
            var segment4 = CreateSimpleSegment(4, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), true);
            var segments = new[] { segment1, segment2, segment3, segment4 };

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(3, node1.Children.Count());
            ClassicAssert.False(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node2 = node1.Children.ElementAt(0);
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(2, node2.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node2.Children.Count());
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.Duration);
            ClassicAssert.False(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node3 = node1.Children.ElementAt(1);
            ClassicAssert.NotNull(node3);
            ClassicAssert.AreEqual(3, node3.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node3.Children.Count());
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(2), node3.Segment.RelativeStartTime);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node3.Segment.Duration);
            ClassicAssert.False(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node4 = node1.Children.ElementAt(2);
            ClassicAssert.NotNull(node4);
            ClassicAssert.AreEqual(4, node4.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node4.Children.Count());
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(5), node4.Segment.RelativeStartTime);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(2), node4.Segment.Duration);
            ClassicAssert.False(node4.Segment.Parameters.ToDictionary().ContainsKey("call_count"));
        }

        [Test]
        public void DoesNotCombineIdenticalNonCombinableSiblings()
        {
            var referenceStartTime = DateTime.Now;

            var segment1 = CreateSimpleSegment(1, null, "foo");
            var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), false);
            var segment3 = CreateSimpleSegment(3, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), false);
            var segments = new[] { segment1, segment2, segment3 };

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            ClassicAssert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            ClassicAssert.NotNull(node1);
            ClassicAssert.AreEqual(1, node1.Segment.UniqueId);
            ClassicAssert.AreEqual(2, node1.Children.Count());
            ClassicAssert.False(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node2 = node1.Children.ElementAt(0);
            ClassicAssert.NotNull(node2);
            ClassicAssert.AreEqual(2, node2.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node2.Children.Count());
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.Duration);
            ClassicAssert.False(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node3 = node1.Children.ElementAt(1);
            ClassicAssert.NotNull(node3);
            ClassicAssert.AreEqual(3, node3.Segment.UniqueId);
            ClassicAssert.AreEqual(0, node3.Children.Count());
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(5), node3.Segment.RelativeStartTime);
            ClassicAssert.AreEqual(TimeSpan.FromSeconds(2), node3.Segment.Duration);
            ClassicAssert.False(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"));
        }

        private static IEnumerable<Segment> CreateSegmentListFromUniqueIds(IEnumerable<KeyValuePair<int, int?>> uniqueIds)
        {
            return uniqueIds
                .Select(kvp => CreateSimpleSegment(kvp.Key, kvp.Value, kvp.Key.ToString()));
        }

        private static Segment CreateSimpleSegment(int uniqueId, int? parentUniqueId, string name, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, bool combinable = false)
        {
            duration = duration ?? TimeSpan.Zero;
            var methodCallData = new MethodCallData("foo", "bar", 1);
            return SimpleSegmentDataTests.createSimpleSegmentBuilder(startTime, duration.Value, uniqueId, parentUniqueId, methodCallData, new Dictionary<string, object>(), name, combinable);
        }
    }
}
