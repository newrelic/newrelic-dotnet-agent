using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Data;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.SystemExtensions.Collections.Generic;
using NewRelic.Testing.Assertions;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	[TestFixture]
	public class SegmentTreeMakerTests
	{
		[NotNull]
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

			Assert.AreEqual(0, treeRoots.Count());
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

			Assert.AreEqual(1, treeRoots.Count);

			var node1 = treeRoots.ElementAt(0);
			Assert.NotNull(node1);
			Assert.AreEqual(0, node1.Children.Count());
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

			Assert.AreEqual(2, treeRoots.Count);

			var node1 = treeRoots.ElementAt(0);
			var node2 = treeRoots.ElementAt(1);
			Assert.NotNull(node1);
			Assert.NotNull(node2);
			Assert.AreEqual(0, node1.Children.Count());
			Assert.AreEqual(0, node2.Children.Count());
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

			Assert.AreEqual(1, treeRoots.Count);

			var node1 = treeRoots.ElementAt(0);
			Assert.NotNull(node1);
			Assert.AreEqual(id1, node1.Segment.UniqueId);
			Assert.AreEqual(1, node1.Children.Count());

			var node2 = node1.Children.ElementAt(0);
			Assert.NotNull(node2);
			Assert.AreEqual(id2, node2.Segment.UniqueId);
			Assert.AreEqual(0, node2.Children.Count());
		}

		[Test]
		public void BuildsComplicatedTrees_IfSeveralSegments()
		{
			/*
			 *
			 *   1    6
			 *   2    7
			 *  5 3
			 *    4
			 *
			 */
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

			Assert.AreEqual(2, treeRoots.Count);

			var node1 = treeRoots.First(node => node.Segment.UniqueId.Equals(1));
			Assert.NotNull(node1);
			Assert.AreEqual(id1, node1.Segment.UniqueId);
			Assert.AreEqual(1, node1.Children.Count());

			var node2 = node1.Children.First(node => node.Segment.UniqueId.Equals(2));
			Assert.NotNull(node2);
			Assert.AreEqual(id2, node2.Segment.UniqueId);
			Assert.AreEqual(2, node2.Children.Count());

			var node3 = node2.Children.First(node => node.Segment.UniqueId.Equals(3));
			Assert.NotNull(node3);
			Assert.AreEqual(id3, node3.Segment.UniqueId);
			Assert.AreEqual(1, node3.Children.Count());

			var node4 = node3.Children.First(node => node.Segment.UniqueId.Equals(4));
			Assert.NotNull(node4);
			Assert.AreEqual(id4, node4.Segment.UniqueId);
			Assert.AreEqual(0, node4.Children.Count());

			var node5 = node2.Children.First(node => node.Segment.UniqueId.Equals(5));
			Assert.NotNull(node5);
			Assert.AreEqual(id5, node5.Segment.UniqueId);
			Assert.AreEqual(0, node5.Children.Count());

			var node6 = treeRoots.First(node => node.Segment.UniqueId.Equals(6));
			Assert.NotNull(node6);
			Assert.AreEqual(id6, node6.Segment.UniqueId);
			Assert.AreEqual(1, node6.Children.Count());

			var node7 = node6.Children.First(node => node.Segment.UniqueId.Equals(7));
			Assert.NotNull(node7);
			Assert.AreEqual(id7, node7.Segment.UniqueId);
			Assert.AreEqual(0, node7.Children.Count());
		}

		[Test]
		public void BuildsComplicatedTrees()
		{
			// This test is a catch-all that should (hopefully) cover every non-error edge case. If this test fails but every other test succeeds then, once the problem is fixed, a new test should be written that explicitly covers that edge case.

			/*
			 *
			 *   1    6
			 *   2    7
			 *  5 3
			 *    4
			 *
			 */
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

 			Assert.AreEqual(2, treeRoots.Count);

			var node1 = treeRoots.First();
			Assert.NotNull(node1);
			Assert.AreEqual(id1, node1.Segment.UniqueId);
			Assert.AreEqual(1, node1.Children.Count());

			var node2 = node1.Children.First();
			Assert.NotNull(node2);
			Assert.AreEqual(id2, node2.Segment.UniqueId);
			Assert.AreEqual(2, node2.Children.Count());

			var node3 = node2.Children.First();
			Assert.NotNull(node3);
			Assert.AreEqual(id3, node3.Segment.UniqueId);
			Assert.AreEqual(1, node3.Children.Count());

			var node4 = node3.Children.First();
			Assert.NotNull(node4);
			Assert.AreEqual(id4, node4.Segment.UniqueId);
			Assert.AreEqual(0, node4.Children.Count());

			var node5 = node2.Children.ElementAt(1);
			Assert.NotNull(node5);
			Assert.AreEqual(id5, node5.Segment.UniqueId);
			Assert.AreEqual(0, node5.Children.Count());

			var node6 = treeRoots.ElementAt(1);
			Assert.NotNull(node6);
			Assert.AreEqual(id6, node6.Segment.UniqueId);
			Assert.AreEqual(1, node6.Children.Count());

			var node7 = node6.Children.First();
			Assert.NotNull(node7);
			Assert.AreEqual(id7, node7.Segment.UniqueId);
			Assert.AreEqual(0, node7.Children.Count());
		}

		[Test]
		public void CombinesIdenticalAdjacentSiblings()
		{
			var segment1 = CreateSimpleSegment(1, null, "foo");
			var segment2 = CreateSimpleSegment(2, 1, "bar", TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), true);
			var segment3 = CreateSimpleSegment(3, 1, "bar", TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(2), true);
			var segments = new[] {segment1, segment2, segment3};

			var treeRoots = _segmentTreeMaker.BuildSegmentTrees(segments).ToList();

			Assert.AreEqual(1, treeRoots.Count);

			var node1 = treeRoots.ElementAt(0);
			Assert.NotNull(node1);
			Assert.AreEqual(1, node1.Segment.UniqueId);
			Assert.AreEqual(1, node1.Children.Count());

			var node2 = node1.Children.ElementAt(0);
			Assert.NotNull(node2);

			NrAssert.Multiple(
				() => Assert.AreEqual(0, node2.Children.Count()),
			
				// The combined segment's start time should be the earliest start time of the original segments
				() => Assert.AreEqual(1, node2.Segment.RelativeStartTime.TotalSeconds),

				// The combined segment's duration should be the sum of the original segments' durations
				() => Assert.AreEqual(TimeSpan.FromSeconds(3), node2.Segment.Duration),

				// call_count parameter should be added and should equal the number of nodes that were combined
				() => Assert.AreEqual(2, node2.Segment.Parameters.ToDictionary()["call_count"])
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

			Assert.AreEqual(1, treeRoots.Count);

			var node1 = treeRoots.ElementAt(0);
			Assert.NotNull(node1);
			Assert.AreEqual(1, node1.Segment.UniqueId);
			Assert.AreEqual(1, node1.Children.Count());

			var node2 = node1.Children.ElementAt(0);
			Assert.NotNull(node2);

			NrAssert.Multiple(
				() => Assert.AreEqual(0, node2.Children.Count()),

				// The combined segment's start time should be the earliest start time of the original segments
				() => Assert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime),

				// The combined segment's duration should be the sum of the original segments' durations
				() => Assert.AreEqual(TimeSpan.FromSeconds(3), node2.Segment.Duration),

				// call_count parameter should be added and should equal the number of nodes that were combined
				() => Assert.AreEqual(2, node2.Segment.Parameters.ToDictionary()["call_count"])
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

            Assert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            Assert.NotNull(node1);
            Assert.AreEqual(1, node1.Segment.UniqueId);
            Assert.AreEqual(3, node1.Children.Count());
            Assert.False(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node2 = node1.Children.ElementAt(0);
            Assert.NotNull(node2);
            Assert.AreEqual(2, node2.Segment.UniqueId);
            Assert.AreEqual(0, node2.Children.Count());
            Assert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.Duration);
            Assert.False(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node3 = node1.Children.ElementAt(1);
            Assert.NotNull(node3);
            Assert.AreEqual(3, node3.Segment.UniqueId);
            Assert.AreEqual(0, node3.Children.Count());
            Assert.AreEqual(TimeSpan.FromSeconds(2), node3.Segment.RelativeStartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(1), node3.Segment.Duration);
            Assert.False(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node4 = node1.Children.ElementAt(2);
            Assert.NotNull(node4);
            Assert.AreEqual(4, node4.Segment.UniqueId);
            Assert.AreEqual(0, node4.Children.Count());
            Assert.AreEqual(TimeSpan.FromSeconds(5), node4.Segment.RelativeStartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(2), node4.Segment.Duration);
            Assert.False(node4.Segment.Parameters.ToDictionary().ContainsKey("call_count"));
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

            Assert.AreEqual(1, treeRoots.Count);

            var node1 = treeRoots.ElementAt(0);
            Assert.NotNull(node1);
            Assert.AreEqual(1, node1.Segment.UniqueId);
            Assert.AreEqual(2, node1.Children.Count());
            Assert.False(node1.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node2 = node1.Children.ElementAt(0);
            Assert.NotNull(node2);
            Assert.AreEqual(2, node2.Segment.UniqueId);
            Assert.AreEqual(0, node2.Children.Count());
            Assert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.RelativeStartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(1), node2.Segment.Duration);
            Assert.False(node2.Segment.Parameters.ToDictionary().ContainsKey("call_count"));

            var node3 = node1.Children.ElementAt(1);
            Assert.NotNull(node3);
            Assert.AreEqual(3, node3.Segment.UniqueId);
            Assert.AreEqual(0, node3.Children.Count());
            Assert.AreEqual(TimeSpan.FromSeconds(5), node3.Segment.RelativeStartTime);
            Assert.AreEqual(TimeSpan.FromSeconds(2), node3.Segment.Duration);
            Assert.False(node3.Segment.Parameters.ToDictionary().ContainsKey("call_count"));
        }

		[NotNull]
		private static IEnumerable<Segment> CreateSegmentListFromUniqueIds([NotNull] IEnumerable<KeyValuePair<int, int?>> uniqueIds)
		{
			return uniqueIds
				.Select(kvp => CreateSimpleSegment(kvp.Key, kvp.Value, kvp.Key.ToString()));
		}

		private static Segment CreateSimpleSegment(int uniqueId, [CanBeNull] int? parentUniqueId, [NotNull] String name, TimeSpan startTime = new TimeSpan(), TimeSpan? duration = null, Boolean combinable = false)
		{
			duration = duration ?? TimeSpan.Zero;
            var methodCallData = new MethodCallData("foo", "bar", 1);
			return SimpleSegmentDataTests.createSimpleSegmentBuilder(startTime, duration.Value, uniqueId, parentUniqueId, methodCallData, new Dictionary<String, Object>(), name, combinable);
		}
	}
}
