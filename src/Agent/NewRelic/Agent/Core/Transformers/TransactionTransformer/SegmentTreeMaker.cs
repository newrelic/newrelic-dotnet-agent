using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MoreLinq;
using NewRelic.Agent.Core.Logging;
using NewRelic.Agent.Core.Time;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.Transformers.TransactionTransformer
{
	public interface ISegmentTreeMaker
	{
		[NotNull]
		IEnumerable<ImmutableSegmentTreeNode> BuildSegmentTrees([NotNull] IEnumerable<Segment> segments);
	}

	public class SegmentTreeMaker : ISegmentTreeMaker
	{

		/// <summary>
		/// Returns the root tree nodes for a flat list of segments.
		/// 
		/// The segments MUST be ordered by creation time so that a parent always preceeds its
		/// children.
		/// </summary>
		/// <param name="segments"></param>
		/// <returns></returns>
		public IEnumerable<ImmutableSegmentTreeNode> BuildSegmentTrees(IEnumerable<Segment> segments)
		{
			int count = segments.Count();
			if (count == 0)
			{ 
				return new ImmutableSegmentTreeNode[0];
			}

			var segmentsArray = segments.ToArray();

			var firstSegment = segmentsArray[0];
			var firstRoot = new SegmentTreeNodeBuilder(firstSegment);
			SegmentTreeNodeBuilder lastSegment = firstRoot;

			var allNodes = new Dictionary<int, SegmentTreeNodeBuilder>();
			allNodes.Add(firstRoot.Segment.UniqueId, firstRoot);

			// initially we track the root nodes excluding the first root.
			var rootNodes = new List<SegmentTreeNodeBuilder>();
			
			for (int i = 1; i < count; i++)
			{
				var segment = segmentsArray[i];

				var segmentNode = new SegmentTreeNodeBuilder(segment);
				allNodes.Add(segment.UniqueId, segmentNode);

				if (!segment.ParentUniqueId.HasValue)
				{
					rootNodes.Add(segmentNode);
				}
				else
				{
					// the parent node may be the last processed node.  If so, skip the dicitonary lookup
					SegmentTreeNodeBuilder parentNode = null;
					if (lastSegment.Segment.UniqueId == segment.ParentUniqueId)
					{
						parentNode = lastSegment;
					}
					else
					{
						if (!allNodes.TryGetValue(segment.ParentUniqueId.Value, out parentNode))
						{
							Log.Finest($"Unable to find parent with id of {segment.ParentUniqueId}");
						}
					}
					if (parentNode != null)
					{
						parentNode.Children.Add(segmentNode);
					}
				}
				lastSegment = segmentNode;
			}

			allNodes.Values.ForEach(CombineSimilarChildren);
			if (rootNodes.Count == 0)
			{
				// if there's only one root, don't use the rootNodes list which will end up allocating an array
				return new ImmutableSegmentTreeNode[] { firstRoot.Build() };
			} else
			{
				rootNodes.Insert(0, firstRoot);
				return rootNodes.Select(node => node.Build());
			}
		}

		private static void CombineSimilarChildren([NotNull] SegmentTreeNodeBuilder node)
		{
			// Look for duplicate neighbors
			for (var index = 0; index < node.Children.Count - 1; index++)
			{
				var child1 = node.Children[index];
				var child2 = node.Children[index + 1];

				if (!child2.Segment.Combinable)
				{
					// if the second child isn't combinable we can jump forward.
					index++;
				}
				else
				{
					// Progressively find and remove all adjacent siblings similar to child 1
					var childrenToCombine = new List<SegmentTreeNodeBuilder> { child1 };
					while (child1.Segment.IsCombinableWith(child2.Segment))
					{
						childrenToCombine.Add(child2);
						node.Children.RemoveAt(index + 1);

						if (index >= node.Children.Count - 1)
							break;

						child2 = node.Children[index + 1];
					}

					// If no similar children were found then keep going
					if (childrenToCombine.Count < 2)
						continue;

					// Replace original node with combined node
					node.Children[index] = GetCombinedNode(childrenToCombine);
				}
			}
		}

		private static SegmentTreeNodeBuilder GetCombinedNode([NotNull] IEnumerable<SegmentTreeNodeBuilder> nodes)
		{
			var nodeList = nodes.Where(node => node != null).ToList();

			if (!nodeList.Any())
				throw new Exception("Need at least one node to combine");
			if (nodeList.Count < 2)
				return nodeList[0];
			
			// Create a segment that has the combined duration of all the other segments
			var earliestStartDate = nodeList.Min(node => node.Segment.RelativeStartTime);
			var totalDuration = nodeList.Sum(node => node.Segment.DurationOrZero);
			var allParameters = nodeList.SelectMany(node => node.Segment.Parameters).ToDictionary();
			allParameters["call_count"] = nodeList.Count;
			var combinedSegment = nodeList[0].Segment.CreateSimilar(earliestStartDate, totalDuration, allParameters);

			// Add all of the other nodes' children into this new combined node
			var combinedNode = new SegmentTreeNodeBuilder(combinedSegment);
			nodeList.SelectMany(node => node.Children).ForEach(combinedNode.Children.Add);

			return combinedNode;
		}
	}

	public class SegmentTreeNodeBuilder
	{
		[NotNull]
		public readonly Segment Segment;

		[NotNull]
		public readonly IList<SegmentTreeNodeBuilder> Children = new List<SegmentTreeNodeBuilder>();

		public SegmentTreeNodeBuilder([NotNull] Segment segment)
		{
			Segment = segment;
		}

		[NotNull]
		public ImmutableSegmentTreeNode Build()
		{
			var childrenNodes = Children.Select(child => child.Build());
			return new ImmutableSegmentTreeNode(Segment, childrenNodes);
		}
	}

	public class ImmutableSegmentTreeNode
	{
		// This class tries to be mostly read-only, but it has to make several concessions due to the nature of the way it is constructed. When a node is built we don't necessarily know all of its children right away, thus we cannot make Children an IEnumerable.
		[NotNull]
		public readonly Segment Segment;

		[NotNull]
		public readonly IEnumerable<ImmutableSegmentTreeNode> Children;

		public readonly TimeSpan TotalChildDuration;

		public ImmutableSegmentTreeNode([NotNull] Segment segment, [NotNull] IEnumerable<ImmutableSegmentTreeNode> children)
		{
			Segment = segment;
			Children = children;
			TotalChildDuration = segment.TotalChildDuration;
		}

		public bool Unfinished { get => Segment.Unfinished; }
	}
}