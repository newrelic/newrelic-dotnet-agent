// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ThreadProfilingServiceTest
    {
        static private UIntPtr[] GenerateStackSnapshot(uint numFunctions, uint start, uint increment, bool randomize = false)
        {
            var functionIds = new UIntPtr[numFunctions];

            for (uint i = 0; i < numFunctions; i++)
            {
                if (randomize)
                {
                    Random rand = new Random(DateTime.UtcNow.Millisecond);
                    uint multiplier = (uint)rand.Next(2, 300);
                    functionIds[i] = new UIntPtr(start + (i * multiplier));
                }
                else
                {
                    functionIds[i] = new UIntPtr(start + (i * increment));
                }
            }

            return functionIds;
        }

        [Test]
        public void verify_single_bucket_serialization()
        { }

        #region Node Pruning

        [Test]
        public void verify_AddNodeToPruningList_really_adds_TreeNode()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(
                null, null);
            ProfileNode node = new ProfileNode(new UIntPtr(10), 1, 2);
            service.AddNodeToPruningList(node);
            Assert.That(service.PruningList, Has.Count.EqualTo(1));
        }

        [Test]
        public void verify_AddNodeToPruningList_adds_correct_number_of_multiple_TreeNodes()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null);
            uint expectedCount = 5;

            for (uint i = 0; i < expectedCount; i++)
            {
                ProfileNode node = new ProfileNode(new UIntPtr(i), 1, 2);
                service.AddNodeToPruningList(node);
            }
            Assert.That(service.PruningList, Has.Count.EqualTo(expectedCount));
        }
        #endregion

        [Test]
        public void verify_ResetCache_clears_all_buckets()
        {
            ThreadProfilingService service = new ThreadProfilingService(null, null);
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(service);
            var fids = GenerateStackSnapshot(10, 200, 50);
            bucket.UpdateTree(fids);

            service.ResetCache();
            Assert.That(service.GetTotalBucketNodeCount(), Is.EqualTo(0));

        }

        [Test]
        public void verify_ResetCache_clears_pruning_list()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null);
            uint expectedCount = 5;

            for (uint i = 0; i < expectedCount; i++)
            {
                ProfileNode node = new ProfileNode(new UIntPtr(i), 1, 2);
                service.AddNodeToPruningList(node);
            }

            service.ResetCache();
            Assert.That(service.PruningList, Is.Empty);
        }

        #region Pruning Tests
        [Test]
        public void verify_PruneTrees_sorts_two_nodes_with_different_call_counts()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, 1);
            ProfileNode node1 = new ProfileNode(new UIntPtr(10), 4, 1);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new UIntPtr(12), 5, 1);
            service.AddNodeToPruningList(node2);

            service.SortPruningTree();
            Assert.That(((ProfileNode)service.PruningList[0]).RunnableCount, Is.GreaterThan(((ProfileNode)service.PruningList[1]).RunnableCount));
        }

        [Test]
        public void verify_PruneTrees_sorts_two_nodes_with_same_call_counts_but_different_depths()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, 1);
            ProfileNode node1 = new ProfileNode(new UIntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new UIntPtr(12), 4, 1);
            service.AddNodeToPruningList(node2);

            service.SortPruningTree();
            Assert.That(((ProfileNode)service.PruningList[0]).Depth < ((ProfileNode)service.PruningList[1]).Depth, Is.True);
        }

        [Test]
        public void verify_PruneTrees_sorts_three_nodes_with_different_call_counts()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, 1);
            ProfileNode node1 = new ProfileNode(new UIntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new UIntPtr(12), 8, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new UIntPtr(16), 2, 2);
            service.AddNodeToPruningList(node3);

            service.SortPruningTree();
            Assert.That(
                (((ProfileNode)service.PruningList[0]).RunnableCount > ((ProfileNode)service.PruningList[1]).RunnableCount) &&
                (((ProfileNode)service.PruningList[1]).RunnableCount > ((ProfileNode)service.PruningList[2]).RunnableCount), Is.True);
        }


        [Test]
        public void verify_PruneTrees_sorts_three_nodes_with_same_call_counts_but_different_depths()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, 1);
            ProfileNode node1 = new ProfileNode(new UIntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new UIntPtr(12), 4, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new UIntPtr(16), 4, 1);
            service.AddNodeToPruningList(node3);

            service.SortPruningTree();
            Assert.That(
                (((ProfileNode)service.PruningList[0]).Depth < ((ProfileNode)service.PruningList[1]).Depth) &&
                (((ProfileNode)service.PruningList[1]).Depth < ((ProfileNode)service.PruningList[2]).Depth), Is.True);
        }

        [Test]
        public void verify_PruneTrees_sets_IgnoreForReporting_flag_to_false_for_nodes_beyond_max_number()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, 3);
            ProfileNode node1 = new ProfileNode(new UIntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new UIntPtr(12), 4, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new UIntPtr(16), 4, 1);
            service.AddNodeToPruningList(node3);

            ProfileNode node4 = new ProfileNode(new UIntPtr(16), 4, 3);
            service.AddNodeToPruningList(node4);

            service.SortPruningTree();

            Assert.Multiple(() =>
            {
                Assert.That(((ProfileNode)service.PruningList[0]).IgnoreForReporting, Is.False);
                Assert.That(((ProfileNode)service.PruningList[1]).IgnoreForReporting, Is.False);
                Assert.That(((ProfileNode)service.PruningList[2]).IgnoreForReporting, Is.False);
                Assert.That(((ProfileNode)service.PruningList[3]).IgnoreForReporting, Is.True);
            });
        }

        #endregion
    }
}
