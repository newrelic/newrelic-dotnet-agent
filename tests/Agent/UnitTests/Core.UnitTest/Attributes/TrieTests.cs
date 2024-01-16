// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core.Attributes.Tests
{
    internal class TestNode
    {
        public string Key;
        public IEnumerable<TestNode> Children = new TestNode[] { };
    }

    [TestFixture]
    public class TrieTests
    {
        [Test]
        public void when_()
        {
            var trieBuilder = new TrieBuilder<string>(
                rootNodeDataFactory: () => "",
                nodeDataMerger: nodeDatas => nodeDatas.First(),
                nodeDataComparor: (first, second) => first.CompareTo(second),
                nodeDataHasher: nodeData => nodeData.GetHashCode(),
                canParentAcceptChildChecker: (parent, child) => child.StartsWith(parent),
                canNodeHaveChildrenChecker: nodeData => true);

            var datas = new[]
            {
                "abi",
                "efg*",
                "a",
                "abc",
                "abc*",
                "abcd",
            };

            var expectedTree = new TestNode
            {
                Key = "",
                Children = new[]
                {
                    new TestNode
                    {
                        Key = "a",
                        Children = new[]
                        {
                            new TestNode
                            {
                                Key = "abc",
                                Children = new[]
                                {
                                    new TestNode { Key = "abc*" },
                                    new TestNode { Key = "abcd" }
                                }
                            },
                            new TestNode { Key = "abi" }
                        }
                    },
                    new TestNode { Key = "efg*" }
                },
            };

            var trie = trieBuilder.CreateTrie(datas);

            AssertTree(expectedTree, trie);
        }

        private void AssertTree(TestNode expected, TrieNode<string> actual)
        {
            ClassicAssert.AreEqual(expected.Key, actual.Data);
            ClassicAssert.AreEqual(expected.Children.Count(), actual.Children.Count);

            foreach (var expectedChild in expected.Children)
            {
                var actualChild = actual.Children
                    .Where(potentialChild => expectedChild.Key == potentialChild.Data)
                    .FirstOrDefault();

                ClassicAssert.NotNull(actualChild);
                AssertTree(expectedChild, actualChild);
            }
        }
    }
}
