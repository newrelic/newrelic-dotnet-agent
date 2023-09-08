// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Core.Logging;
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

        public void UpdateTree(UIntPtr[] fids)
        {
            if (fids == null)
            {
                Log.Debug("fids passed to UpdateTree is null.");
                return;
            }

            lock (_syncObj)
            {
                try
                {
                    UpdateTree(Tree.Root, fids, fids.Length - 1, 0);
                }
                catch (Exception e)
                {
                    Log.Error(e, "UpdateTree() failed");
                }
            }
        }

        private void UpdateTree(ProfileNode parent, UIntPtr[] fids, int fidIndex, uint depth)
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

            UpdateTree(child, fids, fidIndex - 1, ++depth);
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

        internal IEnumerable<UIntPtr> GetFunctionIds()
        {
            return Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
                .Where(node => node != null && node.FunctionId != UIntPtr.Zero)
                .Select(node => node.FunctionId)
                .Distinct();

        }

        public void PopulateNames(IDictionary<UIntPtr, ClassMethodNames> namesSource)
        {
            var nodes = Tree.Root.Flatten(node => node != null ? node.Children : Enumerable.Empty<ProfileNode>())
                .Where(node => node != null);

            foreach (var node in nodes)
            {
                PopulateNames(node, namesSource);
            }
        }

        private void PopulateNames(ProfileNode node, IDictionary<UIntPtr, ClassMethodNames> namesSource)
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
