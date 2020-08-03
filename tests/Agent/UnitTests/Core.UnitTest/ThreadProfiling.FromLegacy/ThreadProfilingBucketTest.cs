// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ThreadProfilingBucketTest
    {
        [Test]
        public void verify_root_node_created_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.IsNotNull(bucket.Tree);
        }

        [Test]
        public void verify_root_node_has_CallCount_of_Zero_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.AreEqual(0, bucket.Tree.Root.RunnableCount);
        }

        [Test]
        public void verify_root_node_has_Depth_of_Zero_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.AreEqual(0, bucket.Tree.Root.Depth);
        }

        [Test]
        public void verify_UpdateTree_handles_null_StackInfo_argument()
        {
            using (var logging = new UnitTest.Fixtures.Logging())
            {
                ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
                bucket.UpdateTree(null, 0);
                Assert.IsTrue(logging.HasMessageBeginingWith("StackInfo passed to UpdateTree is null"));
            }
        }

        #region GetDepth Tests

        [Test]
        public void verify_tree_depth_after_calling_UpdateTree_with_a_one_functionId_stack()
        {
            verify_depth_after_calling_UpdateTree(1);
        }

        [Test]
        public void verify_tree_depth_after_calling_UpdateTree_with_a_six_functionId_stack()
        {
            verify_depth_after_calling_UpdateTree(6);
        }

        [Test]
        public void verify_depth_after_two_calls_to_UpdateTree_with_identical_stacks()
        {
            verify_depth_after_multiple__identical_calls_to_UpdateTree(2, 10);
        }

        [Test]
        public void verify_depth_after_eight_calls_to_UpdateTree_with_identical_stacks()
        {
            verify_depth_after_multiple__identical_calls_to_UpdateTree(8, 15);
        }

        [Test]
        public void verify_All_CallCounts_equal_two_after_two_calls_to_UpdateTree_with_identical_stacks()
        {
            verify_CallCount_after_multiple_calls_to_UpdateTree(2, 12);
        }
        #endregion

        #region GetNodeCount Tests

        [Test]
        public void verify_NodeCount_is_zero_for_empty_tree()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.AreEqual(0, bucket.GetNodeCount());
        }

        [Test]
        public void verify_NodeCount_is_one_for_tree_with_single_node()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(1, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);
            Assert.AreEqual(1, bucket.GetNodeCount());
        }

        [Test]
        public void verify_NodeCount_for_tree_with_two_nodes()
        {
            int numNodes = 2;
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(numNodes, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);
            Assert.AreEqual(numNodes, bucket.GetNodeCount());
        }

        [Test]
        public void verify_NodeCount_for_tree_with_twenty_nodes()
        {
            int numNodes = 20;
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(numNodes, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);
            Assert.AreEqual(numNodes, bucket.GetNodeCount());
        }

        [Test]
        public void verify_NodeCount_is_two_for_tree_with_two_single_node_stacks()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(1, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);

            IntPtr[] functionIds2 = MockStackInfo.GenerateStackSnapshot(1, 125, 25);
            IStackInfo stackInfoIn2 = new MockStackInfo(functionIds2);
            bucket.UpdateTree(stackInfoIn2, 0);
            Assert.AreEqual(2, bucket.GetNodeCount());
        }


        [Test]
        public void verify_NodeCount_for_tree_with_several_multiple_node_stacks_of_diff_depths()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());

            for (int i = 5; i < 30; i = i + 5)
            {
                IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(i, 200, 50, true);
                IStackInfo stackInfoIn = new MockStackInfo(functionIds);
                bucket.UpdateTree(stackInfoIn, 0);
            }

            // Total node count should be equal to 5 + 10 + 15 + 20 + 25 = 75
            Assert.AreEqual(75, bucket.GetNodeCount());
        }

        [Test]
        public void verify_NodeCount_for_tree_with_several_multiple_node_stacks_of_diff_depths_entered_at_various_depths()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());

            for (uint i = 5; i < 30; i = i + 5)
            {
                IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot((int)i, 200, 50, true);
                IStackInfo stackInfoIn = new MockStackInfo(functionIds);
                bucket.UpdateTree(stackInfoIn, i);
            }

            // Total node count should be equal to 5 + 10 + 15 + 20 + 25 = 75
            Assert.AreEqual(75, bucket.GetNodeCount());
        }
        #endregion

        #region Private Helpers

        private void verify_depth_after_calling_UpdateTree(int numFunctionIds)
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(numFunctionIds, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);
            bucket.UpdateTree(stackInfoIn, 0);
            Assert.AreEqual(functionIds.Count(), bucket.GetDepth());

        }

        private void verify_depth_after_multiple__identical_calls_to_UpdateTree(int numCalls, int numFunctionIds)
        {
            // This function verifies that the depth hasn't been changed when identical stacks are added to the tree.

            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(10, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);

            // Use the exact same stack information to update tree multiple times.
            // This should result in the tree's depth not changing after the first call.
            for (int i = 0; i < numCalls; i++)
            {
                bucket.UpdateTree(stackInfoIn, 0);
            }

            Assert.AreEqual(functionIds.Count(), bucket.GetDepth());
        }

        private void verify_CallCount_after_multiple_calls_to_UpdateTree(int numCalls, int numFunctionIds)
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            IntPtr[] functionIds = MockStackInfo.GenerateStackSnapshot(10, 200, 50);
            IStackInfo stackInfoIn = new MockStackInfo(functionIds);

            // Use the exact same stack information to update tree multiple times.
            // This should result in each function's call count being equal to the number
            // of times we update the tree.
            for (int i = 0; i < numCalls; i++)
            {
                // Don't normally reuse a StackInfo but if we do then we need to reset the CurrentIndex
                // since it is decremented by UpdateTree. Good candidate for a refactor.
                stackInfoIn.CurrentIndex = numFunctionIds - 1;
                bucket.UpdateTree(stackInfoIn, 0);
            }

            verify_all_function_CallCounts_match_a_value(bucket, numCalls);
        }

        private void verify_all_function_CallCounts_match_a_value(ThreadProfilingBucket bucket, int expectedCallCount)
        {
            recurse_validate_call_count_for_all(bucket.Tree.Root.Children, expectedCallCount);
        }

        private void recurse_validate_call_count_for_all(ProfileNodes children, int expectedCallCount)
        {
            if (children != null && children.Count() > 0)
            {
                foreach (ProfileNode child in children)
                {
                    Assert.AreEqual(expectedCallCount, child.RunnableCount);
                    recurse_validate_call_count_for_all(child.Children, expectedCallCount);
                }
            }
        }
        #endregion
    }
}
