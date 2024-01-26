// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Collections;
using NUnit.Framework;
using System.Text;

namespace NewRelic.Core.Tests.NewRelic.Collections
{
    internal class Prioritizable : IHasPriority
    {
        public float Priority { get; }

        internal Prioritizable(float priority)
        {
            Priority = priority;
        }
    }
    public class PrioritizedNodeTests
    {
        [Test]
        public void PrioritizedNodeTests_Constructor()
        {
            var prioritizable = new Prioritizable(1.21f);
            var node = new PrioritizedNode<Prioritizable>(prioritizable);
            Assert.That(ReferenceEquals(node.Data, prioritizable), Is.True);
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityNotEqualAsObjects()
        {
            var node1 = (object)new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = (object)new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            Assert.Multiple(() =>
            {
                Assert.That(node1, Is.Not.EqualTo(node2));
                Assert.That(node1, Is.Not.EqualTo(node2));
            });
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityNotEqual()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            Assert.That(node1, Is.Not.EqualTo(node2));
            var result = node1 != node2;
            Assert.That(result, Is.True);
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityHashCodesDiffer()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            Assert.That(node1.GetHashCode(), Is.Not.EqualTo(node2.GetHashCode()));
        }

        [Test]
        public void PrioritizedNodeTests_identityOperatorEqual()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
#pragma warning disable CS1718 // Comparison made to same variable
            var result = node1 == node1;
#pragma warning restore CS1718 // Comparison made to same variable
            Assert.That(result, Is.True);
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityInequality()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var resultGreaterthan = node1 > node2;
            var resultGreaterthanorequal = node1 >= node2;
            var resultLessthan = node1 < node2;
            var resultLessthanorequal = node1 <= node2;

            Assert.Multiple(() =>
            {
                Assert.That(resultGreaterthan, Is.False);
                Assert.That(resultGreaterthanorequal, Is.False);
                Assert.That(resultLessthan, Is.True);
                Assert.That(resultLessthanorequal, Is.True);
            });
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityInequalityNull()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = (PrioritizedNode<Prioritizable>)null;
            Assert.Multiple(() =>
            {
                Assert.That(() => node1 > null, Throws.ArgumentNullException);
                Assert.That(() => node1 >= null, Throws.ArgumentNullException);
                Assert.That(() => node1 <= null, Is.False);
                Assert.That(() => node1 < null, Is.False);
                Assert.That(() => null > node1, Is.False);
                Assert.That(() => null >= node1, Is.False);
                Assert.That(() => null <= node1, Throws.ArgumentNullException);
                Assert.That(() => null < node1, Throws.ArgumentNullException);

                Assert.That(() => node1 > node2, Throws.ArgumentNullException);
                Assert.That(() => node1 >= node2, Throws.ArgumentNullException);
                Assert.That(() => node1 <= node2, Is.False);
                Assert.That(() => node1 < node2, Is.False);
                Assert.That(() => node2 > node1, Is.False);
                Assert.That(() => node2 >= node1, Is.False);
                Assert.That(() => node2 <= node1, Throws.ArgumentNullException);
                Assert.That(() => node2 < node1, Throws.ArgumentNullException);
            });
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityInequalityAsObject()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = (object)new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var result = node1.CompareTo(node2);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.EqualTo(-1));
            });
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityInequalityAsWrongObject()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = (object)new StringBuilder();
            Assert.That(() => node1.CompareTo(node2), Throws.ArgumentException);
        }

        [Test]
        public void PrioritizedNodeTests_SamePriorityInequalityAsNull()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            object node2 = null;
            Assert.That(node1.CompareTo(node2), Is.EqualTo(1));
        }


        [Test]
        public void PrioritizedNodeTests_DifferentPriorityFirstLower()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(5.75f));
            //node2 < node1
            var resultGreaterthan = node1 > node2;
            var resultGreaterthanorequal = node1 >= node2;
            var resultLessthan = node1 < node2;
            var resultLessthanorequal = node1 <= node2;
            var resultEqual = node1 == node2;
            var resultNotEqual = node1 != node2;

            Assert.Multiple(() =>
            {
                Assert.That(resultGreaterthan, Is.True);
                Assert.That(resultGreaterthanorequal, Is.True);
                Assert.That(resultLessthan, Is.False);
                Assert.That(resultLessthanorequal, Is.False);
                Assert.That(resultEqual, Is.False);
                Assert.That(resultNotEqual, Is.True);
            });
        }

        [Test]
        public void PrioritizedNodeTests_DifferentPrioritySecondLower()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(5.75f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            //node1 < node2
            var resultGreaterthan = node1 > node2;
            var resultGreaterthanorequal = node1 >= node2;
            var resultLessthan = node1 < node2;
            var resultLessthanorequal = node1 <= node2;
            var resultEqual = node1 == node2;
            var resultNotEqual = node1 != node2;

            Assert.Multiple(() =>
            {
                Assert.That(resultGreaterthan, Is.False);
                Assert.That(resultGreaterthanorequal, Is.False);
                Assert.That(resultLessthan, Is.True);
                Assert.That(resultLessthanorequal, Is.True);
                Assert.That(resultEqual, Is.False);
                Assert.That(resultNotEqual, Is.True);
            });
        }

        [Test]
        public void PrioritizedNodeTests_DifferentPriorityABAStartHigh()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(5.75f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node3 = new PrioritizedNode<Prioritizable>(new Prioritizable(5.75f));

            //node1 < node3 < node2
            Assert.Multiple(() =>
            {
                Assert.That(node1, Is.LessThanOrEqualTo(node2));
                Assert.That(node1 >= node2, Is.False);
                Assert.That(node1 < node2, Is.True);
                Assert.That(node1, Is.LessThanOrEqualTo(node2));
                Assert.That(node1, Is.Not.EqualTo(node2));
                Assert.That(node1, Is.Not.EqualTo(node2));

                Assert.That(node1, Is.LessThanOrEqualTo(node3));
                Assert.That(node1 >= node3, Is.False);
                Assert.That(node1 < node3, Is.True);
                Assert.That(node1, Is.LessThanOrEqualTo(node3));
                Assert.That(node1, Is.Not.EqualTo(node3));
                Assert.That(node1, Is.Not.EqualTo(node3));

                Assert.That(node2, Is.GreaterThan(node3));
                Assert.That(node2, Is.GreaterThanOrEqualTo(node3));
                Assert.That(node2, Is.GreaterThanOrEqualTo(node3));
                Assert.That(node2, Is.GreaterThan(node3));
                Assert.That(node2, Is.Not.EqualTo(node3));
                Assert.That(node2, Is.Not.EqualTo(node3));
            }
            );
        }

        [Test]
        public void PrioritizedNodeTests_DifferentPriorityABAStartLow()
        {
            var node1 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            var node2 = new PrioritizedNode<Prioritizable>(new Prioritizable(5.75f));
            var node3 = new PrioritizedNode<Prioritizable>(new Prioritizable(1.21f));
            //node2 < node1 < node3
            Assert.Multiple(() =>
                {
                    Assert.That(node1, Is.GreaterThan(node2));
                    Assert.That(node1, Is.GreaterThanOrEqualTo(node2));
                    Assert.That(node1, Is.GreaterThanOrEqualTo(node2));
                    Assert.That(node1, Is.GreaterThan(node2));
                    Assert.That(node1, Is.Not.EqualTo(node2));
                    Assert.That(node1, Is.Not.EqualTo(node2));

                    Assert.That(node1, Is.LessThanOrEqualTo(node3));
                    Assert.That(node1 >= node3, Is.False);
                    Assert.That(node1 < node3, Is.True);
                    Assert.That(node1, Is.LessThanOrEqualTo(node3));
                    Assert.That(node1, Is.Not.EqualTo(node3));
                    Assert.That(node1, Is.Not.EqualTo(node3));

                    Assert.That(node2, Is.LessThanOrEqualTo(node3));
                    Assert.That(node2 >= node3, Is.False);
                    Assert.That(node2 < node3, Is.True);
                    Assert.That(node2, Is.LessThanOrEqualTo(node3));
                    Assert.That(node2, Is.Not.EqualTo(node3));
                    Assert.That(node2, Is.Not.EqualTo(node3));
                }
            );
        }
    }
}
