using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.ThreadProfiling
{
	/// <summary>
	/// A tree of function identifiers representing function calls in a stack.
	/// </summary>
	public class ThreadProfilingBucket
	{
		private const String NativeClassDescriptiveName = "Native";
		private const String NativeFunctionDescriptiveName = "Function Call";
		private const String UnknownClassName = "UnknownClass";
		private const String UnknownMethodName = "UnknownMethod";

		[NotNull]
		public readonly BucketProfile Tree;
		[NotNull]
		private readonly Object _syncObj = new Object();

		[NotNull]
		private readonly IThreadProfilingProcessing _service;
		
		public ThreadProfilingBucket([NotNull] IThreadProfilingProcessing service)
		{
			_service = service;
			Tree = new BucketProfile();
		}

		public void ClearTree()
		{
			lock (_syncObj)
			{
				Tree.Root.ClearChildren();
			}
		}

		public void UpdateTree(UIntPtr[] fids)
		{
			if (fids == null)
			{
				Log.DebugFormat("fids passed to UpdateTree is null.");
				return;
			}

			lock (_syncObj)
			{
				try
				{
					UpdateTree(Tree.Root, fids, fids.Length-1, 0);
				}
				catch (Exception e)
				{
					Log.ErrorFormat("{0}", e);
				}
			}
		}

		private void UpdateTree([NotNull] ProfileNode parent, [NotNull] UIntPtr[] fids, int fidIndex, UInt32 depth)
		{
			if (fidIndex < 0)
				return;

			var fid = fids[fidIndex];
			var child = parent.Children
				.Where(node => node != null)
				.Where(node => node.FunctionId == fid)
				.FirstOrDefault();

			if (child != null)
			{
				child.RunnableCount++;
			}
			else
			{
				// If no matching child found, create a new one to recurse into
				child = new ProfileNode(fid, 1, depth);
				parent.AddChild(child);

				// If we just added this node's only child, add it to the pruning list
				if (parent.Children.Count == 1)
					_service.AddNodeToPruningList(child);
			}

			UpdateTree(child, fids, fidIndex-1, ++depth);
		}

		public Int32 GetNodeCount()
		{
			var totalNodeCount = Tree.Root
				.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
				.Count();

			// Root is not included in node count
			return totalNodeCount - 1;
		}

		public Int32 GetDepth()
		{
			var treeDepth = GetDepth(Tree.Root, 0);

			// Root is not included in depth count
			return treeDepth - 1;
		}

		private static Int32 GetDepth([NotNull] ProfileNode node, Int32 currentDepth)
		{
			currentDepth++;

			if (node.Children.Count < 1)
				return currentDepth;

			return node.Children
				.Where(child => child != null)
				.Select(child => GetDepth(child, currentDepth))
				.Max();
		}

		internal IEnumerable<UIntPtr> GetFunctionIds()
		{
			return Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
				.Where(node => node != null && node.FunctionId != UIntPtr.Zero)
				.Select(node => node.FunctionId)
				.Distinct();

		}

		public void PopulateNames([NotNull] IDictionary<UIntPtr, ClassMethodNames> namesSource)
		{
			var nodes = Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
				.Where(node => node != null);

			foreach (var node in nodes)
			{
				PopulateNames(node, namesSource);
			}
		}

		private void PopulateNames([NotNull] ProfileNode node, [NotNull] IDictionary<UIntPtr, ClassMethodNames> namesSource)
		{
			if (node.FunctionId == UIntPtr.Zero)
			{
				node.Details.ClassName = NativeClassDescriptiveName;
				node.Details.MethodName = NativeFunctionDescriptiveName;
			}
			else
			{
				var id = node.FunctionId;
				if (namesSource.ContainsKey(id) && namesSource[id] != null)
				{
					node.Details.ClassName = namesSource[id].Class;
					node.Details.MethodName = namesSource[id].Method;
				}
				else
				{
					node.Details.ClassName = UnknownClassName;
					node.Details.MethodName = UnknownMethodName + '(' + id + ')';
				}
			}
		}

		public void PruneTree()
		{
			lock (_syncObj)
			{
				PruneTree(Tree.Root);
			}
		}

		private static void PruneTree([NotNull] ProfileNode node)
		{
			if (node.Children.Count <= 0)
				return;

			node.Children
				.Where(child => child != null)
				.Where(child => child.IgnoreForReporting)
				.ToList()
				.ForEach(child => node.Children.Remove(child));

			foreach (var kid in node.Children) PruneTree(kid);
		}

	}
}
