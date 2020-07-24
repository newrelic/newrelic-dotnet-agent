using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ThreadProfilingServiceTest
    {
        [Test]
        public void verify_single_bucket_serialization()
        { }

        #region Node Pruning

        [Test]
        public void verify_AddNodeToPruningList_really_adds_TreeNode()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null);
            ProfileNode node = new ProfileNode(new IntPtr(10), 1, 2);
            service.AddNodeToPruningList(node);
            Assert.AreEqual(1, service.PruningList.Count);
        }

        [Test]
        public void verify_AddNodeToPruningList_adds_correct_number_of_multiple_TreeNodes()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null);
            int expectedCount = 5;

            for (int i = 0; i < expectedCount; i++)
            {
                ProfileNode node = new ProfileNode(new IntPtr(i), 1, 2);
                service.AddNodeToPruningList(node);
            }
            Assert.AreEqual(expectedCount, service.PruningList.Count);
        }
        #endregion

        [Test]
        public void verify_ResetCache_clears_all_buckets()
        {
            ThreadProfilingService service = new ThreadProfilingService(null, null, null, null);
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(service);
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(10, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);

            service.ResetCache();
            Assert.AreEqual(0, service.GetTotalBucketNodeCount());

        }

        [Test]
        public void verify_ResetCache_clears_pruning_list()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null);
            int expectedCount = 5;

            for (int i = 0; i < expectedCount; i++)
            {
                ProfileNode node = new ProfileNode(new IntPtr(i), 1, 2);
                service.AddNodeToPruningList(node);
            }

            service.ResetCache();
            Assert.AreEqual(0, service.PruningList.Count);
        }

        #region Pruning Tests
        [Test]
        public void verify_PruneTrees_sorts_two_nodes_with_different_call_counts()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null, 1);
            ProfileNode node1 = new ProfileNode(new IntPtr(10), 4, 1);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new IntPtr(12), 5, 1);
            service.AddNodeToPruningList(node2);

            service.SortPruningTree();
            Assert.IsTrue(((ProfileNode)service.PruningList[0]).RunnableCount > ((ProfileNode)service.PruningList[1]).RunnableCount);
        }

        [Test]
        public void verify_PruneTrees_sorts_two_nodes_with_same_call_counts_but_different_depths()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null, 1);
            ProfileNode node1 = new ProfileNode(new IntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new IntPtr(12), 4, 1);
            service.AddNodeToPruningList(node2);

            service.SortPruningTree();
            Assert.IsTrue(((ProfileNode)service.PruningList[0]).Depth < ((ProfileNode)service.PruningList[1]).Depth);
        }

        [Test]
        public void verify_PruneTrees_sorts_three_nodes_with_different_call_counts()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null, 1);
            ProfileNode node1 = new ProfileNode(new IntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new IntPtr(12), 8, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new IntPtr(16), 2, 2);
            service.AddNodeToPruningList(node3);

            service.SortPruningTree();
            Assert.IsTrue(
                (((ProfileNode)service.PruningList[0]).RunnableCount > ((ProfileNode)service.PruningList[1]).RunnableCount) &&
                (((ProfileNode)service.PruningList[1]).RunnableCount > ((ProfileNode)service.PruningList[2]).RunnableCount));
        }


        [Test]
        public void verify_PruneTrees_sorts_three_nodes_with_same_call_counts_but_different_depths()
        {
            // Set the max aggregated nodes to 1 so that pruning is triggered.
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null, 1);
            ProfileNode node1 = new ProfileNode(new IntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new IntPtr(12), 4, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new IntPtr(16), 4, 1);
            service.AddNodeToPruningList(node3);

            service.SortPruningTree();
            Assert.IsTrue(
                (((ProfileNode)service.PruningList[0]).Depth < ((ProfileNode)service.PruningList[1]).Depth) &&
                (((ProfileNode)service.PruningList[1]).Depth < ((ProfileNode)service.PruningList[2]).Depth));
        }

        [Test]
        public void verify_PruneTrees_sets_IgnoreForReporting_flag_to_false_for_nodes_beyond_max_number()
        {
            IThreadProfilingProcessing service = new ThreadProfilingService(null, null, null, null, 3);
            ProfileNode node1 = new ProfileNode(new IntPtr(10), 4, 2);
            service.AddNodeToPruningList(node1);

            ProfileNode node2 = new ProfileNode(new IntPtr(12), 4, 6);
            service.AddNodeToPruningList(node2);

            ProfileNode node3 = new ProfileNode(new IntPtr(16), 4, 1);
            service.AddNodeToPruningList(node3);

            ProfileNode node4 = new ProfileNode(new IntPtr(16), 4, 3);
            service.AddNodeToPruningList(node4);

            service.SortPruningTree();

            Assert.IsFalse(((ProfileNode)service.PruningList[0]).IgnoreForReporting);
            Assert.IsFalse(((ProfileNode)service.PruningList[1]).IgnoreForReporting);
            Assert.IsFalse(((ProfileNode)service.PruningList[2]).IgnoreForReporting);
            Assert.IsTrue(((ProfileNode)service.PruningList[3]).IgnoreForReporting);
        }

        #endregion
    }
}
