using System;
using NUnit.Framework;

namespace NewRelic.Agent.Core.ThreadProfiling
{
    [TestFixture]
    public class ProfileNodeTest
    {
        [Test]
        public void verify_IntPtr_value_on_creation_of_a_new_tree_node()
        {
            IntPtr ipTest = new IntPtr(10);
            ProfileNode node = new ProfileNode(ipTest, 1, 0);
            Assert.AreEqual(new IntPtr(10), node.FunctionId);
        }

        [Test]
        public void verify_CallCount_value_on_creation_of_a_new_tree_node()
        {
            ProfileNode node = new ProfileNode(new IntPtr(10), 35, 0);
            Assert.AreEqual(35, node.RunnableCount);
        }

        [Test]
        public void verify_first_child_added()
        {
            IntPtr ipTest = new IntPtr(20);
            ProfileNode node = new ProfileNode(new IntPtr(), 1, 0);
            node.AddChild(new ProfileNode(ipTest, 1, 0));
            Assert.AreEqual(1, node.Children.Count);
        }

        [Test]
        public void verify_first_child_added_has_correct_FunctionId()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            const int IP_CHILD_VALUE = 175275300;
            IntPtr ipExpected = new IntPtr(IP_CHILD_VALUE);
            IntPtr ipChild = new IntPtr(IP_CHILD_VALUE);
            rootProfileNode.AddChild(new ProfileNode(ipChild, 1, 1));
            Assert.AreEqual(ipExpected, GetProfileNodeChildByIndex(rootProfileNode, 0).FunctionId);
        }

        [Test]
        public void verify_first_child_added_has_correct_CallCount_value()
        {
            IntPtr ipRoot = new IntPtr(1);
            const int EXPECTED_CALL_COUNT = 12;
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 0, 0);

            rootProfileNode.AddChild(new ProfileNode(new IntPtr(1), EXPECTED_CALL_COUNT, 1));
            Assert.AreEqual(EXPECTED_CALL_COUNT, GetProfileNodeChildByIndex(rootProfileNode, 0).RunnableCount);
        }

        [Test]
        public void verify_first_child_added_has_correct_Depth_value()
        {
            IntPtr ipRoot = new IntPtr(1);
            const int EXPECTED_DEPTH_VALUE = 2;
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            rootProfileNode.AddChild(new ProfileNode(new IntPtr(1), 1, EXPECTED_DEPTH_VALUE));
            Assert.AreEqual(EXPECTED_DEPTH_VALUE, GetProfileNodeChildByIndex(rootProfileNode, 0).Depth);
        }

        [Test]
        public void verify_second_child_added()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            // Add first child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(2), 1, 1));

            // Add second child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(3), 1, 1));
            Assert.AreEqual(2, rootProfileNode.Children.Count);
        }

        [Test]
        public void verify_second_child_added_has_correct_FunctionId()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            // Add first child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(2), 1, 1));

            const int IP_CHILD_VALUE = 175275300;
            IntPtr ipExpected = new IntPtr(IP_CHILD_VALUE);
            IntPtr ipChild = new IntPtr(IP_CHILD_VALUE);

            // Add second child.
            rootProfileNode.AddChild(new ProfileNode(ipChild, 1, 1));
            Assert.AreEqual(ipExpected, GetProfileNodeChildByIndex(rootProfileNode, 1).FunctionId);
        }

        [Test]
        public void verify_second_child_added_has_correct_CallCount()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            // Add first child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(2), 1, 1));

            const int EXPECTED_CALL_COUNT = 12;

            // Add second child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(11), EXPECTED_CALL_COUNT, 1));
            Assert.AreEqual(EXPECTED_CALL_COUNT, GetProfileNodeChildByIndex(rootProfileNode, 1).RunnableCount);
        }

        [Test]
        public void verify_second_child_added_has_correct_Depth()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            // Add first child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(2), 1, 1));

            const int EXPECTED_DEPTH = 6;

            // Add second child.
            rootProfileNode.AddChild(new ProfileNode(new IntPtr(11), 1, EXPECTED_DEPTH));
            Assert.AreEqual(EXPECTED_DEPTH, GetProfileNodeChildByIndex(rootProfileNode, 1).Depth);
        }

        [Test]
        public void verify_grandchild_added()
        {
            IntPtr ipRoot = new IntPtr(1);
            ProfileNode rootProfileNode = new ProfileNode(ipRoot, 1, 0);

            // Add first child.
            ProfileNode child = new ProfileNode(new IntPtr(2), 1, 1);
            rootProfileNode.AddChild(child);

            // Add grandchild.
            child.AddChild(new ProfileNode(new IntPtr(3), 1, 1));
            Assert.AreEqual(1, GetProfileNodeChildByIndex(rootProfileNode, 0).Children.Count);
        }

        private ProfileNode GetProfileNodeChildByIndex(ProfileNode parent, int index)
        {
            ProfileNode node = null;
            if (index < parent.Children.Count)
            {
                int count = 0;
                foreach (ProfileNode n in parent.Children)
                {
                    if (count++ == index)
                    {
                        node = n;
                        break;
                    }
                }
            }
            return node;
        }
    }
}
