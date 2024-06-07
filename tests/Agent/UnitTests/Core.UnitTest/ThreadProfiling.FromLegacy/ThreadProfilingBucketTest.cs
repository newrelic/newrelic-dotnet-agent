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
        public void verify_root_node_created_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.That(bucket.Tree, Is.Not.Null);
        }

        [Test]
        public void verify_root_node_has_CallCount_of_Zero_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.That(bucket.Tree.Root.RunnableCount, Is.EqualTo(0));
        }

        [Test]
        public void verify_root_node_has_Depth_of_Zero_on_construction()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            Assert.That(bucket.Tree.Root.Depth, Is.EqualTo(0));
        }

        [Test]
        public void verify_UpdateTree_handles_null_StackInfo_argument()
        {
            using (var logging = new TestUtilities.Logging())
            {
                ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
                bucket.UpdateTree(null);
                Assert.That(logging.HasMessageBeginningWith("fids passed to UpdateTree is null"), Is.True);
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
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(0));
        }

        [Test]
        public void verify_NodeCount_is_one_for_tree_with_single_node()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(1, 200, 50);
            bucket.UpdateTree(fids);
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(1));
        }

        [Test]
        public void verify_NodeCount_for_tree_with_two_nodes()
        {
            uint numNodes = 2;
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(numNodes, 200, 50);
            bucket.UpdateTree(fids);
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(numNodes));
        }

        [Test]
        public void verify_NodeCount_for_tree_with_twenty_nodes()
        {
            uint numNodes = 20;
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(numNodes, 200, 50);
            bucket.UpdateTree(fids);
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(numNodes));
        }

        [Test]
        public void verify_NodeCount_is_two_for_tree_with_two_single_node_stacks()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(1, 200, 50);
            bucket.UpdateTree(fids);

            var fids2 = GenerateStackSnapshot(1, 125, 25);
            bucket.UpdateTree(fids2);
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(2));
        }


        [Test]
        public void verify_NodeCount_for_tree_with_several_multiple_node_stacks_of_diff_depths()
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());

            for (uint i = 5; i < 30; i += 5)
            {
                var fids = GenerateStackSnapshot(i, 200, 50, true);
                bucket.UpdateTree(fids);
            }

            // Total node count should be equal to 5 + 10 + 15 + 20 + 25 = 75
            Assert.That(bucket.GetNodeCount(), Is.EqualTo(75));
        }

        #endregion

        #region Private Helpers

        private void verify_depth_after_calling_UpdateTree(uint numFunctionIds)
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(numFunctionIds, 200, 50);
            bucket.UpdateTree(fids);
            Assert.That(bucket.GetDepth(), Is.EqualTo(fids.Count()));

        }

        private void verify_depth_after_multiple__identical_calls_to_UpdateTree(int numCalls, int numFunctionIds)
        {
            // This function verifies that the depth hasn't been changed when identical stacks are added to the tree.

            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(10, 200, 50);

            // Use the exact same stack information to update tree multiple times.
            // This should result in the tree's depth not changing after the first call.
            for (int i = 0; i < numCalls; i++)
            {
                bucket.UpdateTree(fids);
            }

            Assert.That(bucket.GetDepth(), Is.EqualTo(fids.Count()));
        }

        private void verify_CallCount_after_multiple_calls_to_UpdateTree(int numCalls, int numFunctionIds)
        {
            ThreadProfilingBucket bucket = new ThreadProfilingBucket(new MockThreadProfilingService());
            var fids = GenerateStackSnapshot(10, 200, 50);

            // Use the exact same stack information to update tree multiple times.
            // This should result in each function's call count being equal to the number
            // of times we update the tree.
            for (int i = 0; i < numCalls; i++)
            {
                // Don't normally reuse a StackInfo but if we do then we need to reset the CurrentIndex
                // since it is decremented by UpdateTree. Good candidate for a refactor.
                bucket.UpdateTree(fids.Take(numFunctionIds - 1).ToArray());
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
                    Assert.That(child.RunnableCount, Is.EqualTo(expectedCallCount));
                    recurse_validate_call_count_for_all(child.Children, expectedCallCount);
                }
            }
        }
        #endregion
    }
}
