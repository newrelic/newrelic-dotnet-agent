/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Logging;
using NewRelic.SystemExtensions.Collections.Generic;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    /// <summary>
    /// A tree of function identifiers representing function calls in a stack.
    /// </summary>
    public class ThreadProfilingBucket
    {
        private const string NativeClassDescriptiveName = "Native";
        private const string NativeFunctionDescriptiveName = "Function Call";
        private const string UnknownClassName = "UnknownClass";
        private const string UnknownMethodName = "UnknownMethod";
        public readonly BucketProfile Tree;
        private readonly object _syncObj = new object();
        private readonly IThreadProfilingProcessing _service;

        public ThreadProfilingBucket(IThreadProfilingProcessing service)
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

        public void UpdateTree(IStackInfo stackInfo, uint depth)
        {
            if (stackInfo == null)
            {
                Log.DebugFormat("StackInfo passed to UpdateTree is null. Depth was {0}", depth);
                return;
            }

            lock (_syncObj)
            {
                try
                {
                    UpdateTree(Tree.Root, stackInfo, depth);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("{0}", e);
                }
            }
        }

        private void UpdateTree(ProfileNode parent, IStackInfo stackInfo, uint depth)
        {
            if (stackInfo.CurrentIndex < 0)
                return;

            var child = parent.Children
                .Where(node => node != null)
                .Where(node => node.FunctionId == stackInfo.FunctionId)
                .FirstOrDefault();

            if (child != null)
            {
                child.RunnableCount++;
            }
            else
            {
                // If no matching child found, create a new one to recurse into
                child = new ProfileNode(stackInfo.FunctionId, 1, depth);
                parent.AddChild(child);

                // If we just added this node's only child, add it to the pruning list
                if (parent.Children.Count == 1)
                    _service.AddNodeToPruningList(child);
            }

            stackInfo.CurrentIndex--;
            UpdateTree(child, stackInfo, ++depth);
        }

        public int GetNodeCount()
        {
            var totalNodeCount = Tree.Root
                .Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
                .Count();

            // Root is not included in node count
            return totalNodeCount - 1;
        }

        public int GetDepth()
        {
            var treeDepth = GetDepth(Tree.Root, 0);

            // Root is not included in depth count
            return treeDepth - 1;
        }

        private static int GetDepth(ProfileNode node, int currentDepth)
        {
            currentDepth++;

            if (node.Children.Count < 1)
                return currentDepth;

            return node.Children
                .Where(child => child != null)
                .Select(child => GetDepth(child, currentDepth))
                .Max();
        }

        internal HashSet<ulong> GetFunctionIds()
        {
            var nodes = Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
                .Where(node => node != null);

            HashSet<ulong> functionIds = new HashSet<ulong>();
            foreach (var node in nodes)
            {
                if (node.FunctionId != IntPtr.Zero)
                {
                    functionIds.Add((ulong)node.FunctionId.ToInt64());
                }
            }
            return functionIds;
        }

        public void PopulateNames(IDictionary<ulong, ClassMethodNames> namesSource)
        {
            var nodes = Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
                .Where(node => node != null);

            foreach (var node in nodes)
            {
                PopulateNames(node, namesSource);
            }
        }

        private void PopulateNames(ProfileNode node, IDictionary<ulong, ClassMethodNames> namesSource)
        {
            if (node.FunctionId == IntPtr.Zero)
            {
                node.Details.ClassName = NativeClassDescriptiveName;
                node.Details.MethodName = NativeFunctionDescriptiveName;
            }
            else
            {
                var id = (ulong)node.FunctionId.ToInt64();
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

        private static void PruneTree(ProfileNode node)
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
