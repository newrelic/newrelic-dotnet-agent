// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Segments;
using NewRelic.Agent.Core.Segments.Tests;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

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

            Assert.That(treeRoots.Count(), Is.EqualTo(0));
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

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.That(node1.Children.Count(), Is.EqualTo(0));
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

            Assert.That(treeRoots, Has.Count.EqualTo(2));

            var node1 = treeRoots.ElementAt(0);
            var node2 = treeRoots.ElementAt(1);
            Assert.Multiple(() =>
            {
                Assert.That(node1, Is.Not.Null);
                Assert.That(node2, Is.Not.Null);
            });
            Assert.Multiple(() =>
            {
                Assert.That(node1.Children.Count(), Is.EqualTo(0));
                Assert.That(node2.Children.Count(), Is.EqualTo(0));
            });
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

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(id1));
                Assert.That(node1.Children.Count(), Is.EqualTo(1));
            });

            var node2 = node1.Children.ElementAt(0);
            Assert.That(node2, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node2.Segment.UniqueId, Is.EqualTo(id2));
                Assert.That(node2.Children.Count(), Is.EqualTo(0));
            });
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

            Assert.That(treeRoots, Has.Count.EqualTo(2));

            var node1 = treeRoots.First(node => node.Segment.UniqueId.Equals(1));
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(id1));
                Assert.That(node1.Children.Count(), Is.EqualTo(1));
            });

            var node2 = node1.Children.First(node => node.Segment.UniqueId.Equals(2));
            Assert.That(node2, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node2.Segment.UniqueId, Is.EqualTo(id2));
                Assert.That(node2.Children.Count(), Is.EqualTo(2));
            });

            var node3 = node2.Children.First(node => node.Segment.UniqueId.Equals(3));
            Assert.That(node3, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node3.Segment.UniqueId, Is.EqualTo(id3));
                Assert.That(node3.Children.Count(), Is.EqualTo(1));
            });

            var node4 = node3.Children.First(node => node.Segment.UniqueId.Equals(4));
            Assert.That(node4, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node4.Segment.UniqueId, Is.EqualTo(id4));
                Assert.That(node4.Children.Count(), Is.EqualTo(0));
            });

            var node5 = node2.Children.First(node => node.Segment.UniqueId.Equals(5));
            Assert.That(node5, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node5.Segment.UniqueId, Is.EqualTo(id5));
                Assert.That(node5.Children.Count(), Is.EqualTo(0));
            });

            var node6 = treeRoots.First(node => node.Segment.UniqueId.Equals(6));
            Assert.That(node6, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node6.Segment.UniqueId, Is.EqualTo(id6));
                Assert.That(node6.Children.Count(), Is.EqualTo(1));
            });

            var node7 = node6.Children.First(node => node.Segment.UniqueId.Equals(7));
            Assert.That(node7, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node7.Segment.UniqueId, Is.EqualTo(id7));
                Assert.That(node7.Children.Count(), Is.EqualTo(0));
            });
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

            Assert.That(treeRoots, Has.Count.EqualTo(2));

            var node1 = treeRoots.First();
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(id1));
                Assert.That(node1.Children.Count(), Is.EqualTo(1));
            });

            var node2 = node1.Children.First();
            Assert.That(node2, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node2.Segment.UniqueId, Is.EqualTo(id2));
                Assert.That(node2.Children.Count(), Is.EqualTo(2));
            });

            var node3 = node2.Children.First();
            Assert.That(node3, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node3.Segment.UniqueId, Is.EqualTo(id3));
                Assert.That(node3.Children.Count(), Is.EqualTo(1));
            });

            var node4 = node3.Children.First();
            Assert.That(node4, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node4.Segment.UniqueId, Is.EqualTo(id4));
                Assert.That(node4.Children.Count(), Is.EqualTo(0));
            });

            var node5 = node2.Children.ElementAt(1);
            Assert.That(node5, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node5.Segment.UniqueId, Is.EqualTo(id5));
                Assert.That(node5.Children.Count(), Is.EqualTo(0));
            });

            var node6 = treeRoots.ElementAt(1);
            Assert.That(node6, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node6.Segment.UniqueId, Is.EqualTo(id6));
                Assert.That(node6.Children.Count(), Is.EqualTo(1));
            });

            var node7 = node6.Children.First();
            Assert.That(node7, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node7.Segment.UniqueId, Is.EqualTo(id7));
                Assert.That(node7.Children.Count(), Is.EqualTo(0));
            });
        }

        [Test]
        public void CombinesIdenticalAdjacentSiblings()
        {
            var segment1 = CreateSimpleSegment(1, null, "foo");
            var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), true);
            var segment3 = CreateSimpleSegment(3, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), true);
            var segments = new[] { segment1, segment2, segment3 };

            var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(1));
                Assert.That(node1.Children.Count(), Is.EqualTo(1));
            });

            var node2 = node1.Children.ElementAt(0);
            Assert.That(node2, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(node2.Children.Count(), Is.EqualTo(0)),

                // The combined segment's start time should be the earliest start time of the original segments
                () => Assert.That(node2.Segment.RelativeStartTime.TotalSeconds, Is.EqualTo(1)),

                // The combined segment's duration should be the sum of the original segments' durations
                () => Assert.That(node2.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(3))),

                // call_count parameter should be added and should equal the number of nodes that were combined
                () => Assert.That(node2.Segment.Parameters.ToDictionary()["call_count"], Is.EqualTo(2))
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

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(1));
                Assert.That(node1.Children.Count(), Is.EqualTo(1));
            });

            var node2 = node1.Children.ElementAt(0);
            Assert.That(node2, Is.Not.Null);

            NrAssert.Multiple(
                () => Assert.That(node2.Children.Count(), Is.EqualTo(0)),

                // The combined segment's start time should be the earliest start time of the original segments
                () => Assert.That(node2.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(1))),

                // The combined segment's duration should be the sum of the original segments' durations
                () => Assert.That(node2.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(3))),

                // call_count parameter should be added and should equal the number of nodes that were combined
                () => Assert.That(node2.Segment.Parameters.ToDictionary()["call_count"], Is.EqualTo(2))
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

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(1));
                Assert.That(node1.Children.Count(), Is.EqualTo(3));
            });
            Assert.That(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);

            var node2 = node1.Children.ElementAt(0);
            Assert.That(node2, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node2.Segment.UniqueId, Is.EqualTo(2));
                Assert.That(node2.Children.Count(), Is.EqualTo(0));
                Assert.That(node2.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(node2.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
            });
            Assert.That(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);

            var node3 = node1.Children.ElementAt(1);
            Assert.That(node3, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node3.Segment.UniqueId, Is.EqualTo(3));
                Assert.That(node3.Children.Count(), Is.EqualTo(0));
                Assert.That(node3.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(2)));
                Assert.That(node3.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
            });
            Assert.That(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);

            var node4 = node1.Children.ElementAt(2);
            Assert.That(node4, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node4.Segment.UniqueId, Is.EqualTo(4));
                Assert.That(node4.Children.Count(), Is.EqualTo(0));
                Assert.That(node4.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(node4.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            });
            Assert.That(node4.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);
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

            Assert.That(treeRoots, Has.Count.EqualTo(1));

            var node1 = treeRoots.ElementAt(0);
            Assert.That(node1, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node1.Segment.UniqueId, Is.EqualTo(1));
                Assert.That(node1.Children.Count(), Is.EqualTo(2));
            });
            Assert.That(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);

            var node2 = node1.Children.ElementAt(0);
            Assert.That(node2, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node2.Segment.UniqueId, Is.EqualTo(2));
                Assert.That(node2.Children.Count(), Is.EqualTo(0));
                Assert.That(node2.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(1)));
                Assert.That(node2.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(1)));
            });
            Assert.That(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);

            var node3 = node1.Children.ElementAt(1);
            Assert.That(node3, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(node3.Segment.UniqueId, Is.EqualTo(3));
                Assert.That(node3.Children.Count(), Is.EqualTo(0));
                Assert.That(node3.Segment.RelativeStartTime, Is.EqualTo(TimeSpan.FromSeconds(5)));
                Assert.That(node3.Segment.Duration, Is.EqualTo(TimeSpan.FromSeconds(2)));
            });
            Assert.That(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"), Is.False);
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
            return SimpleSegmentDataTestHelpers.CreateSimpleSegmentBuilder(startTime, duration.Value, uniqueId, parentUniqueId, methodCallData, new Dictionary<string, object>(), name, combinable);
        }
    }
}
