// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.SystemExtensions.Collections.Generic;
using NUnit.Framework;


namespace NewRelic.SystemExtensions.UnitTests.Collections.Generic
{
    public class IEnumerableExtensionsTests
    {
        #region Unless(Func<T,bool>)


        [TestCase(new int[0], new int[0])]
        [TestCase(new[] { 1 }, new[] { 1 })]
        [TestCase(new[] { 9 }, new int[0])]
        [TestCase(new[] { 1, 9, 2 }, new[] { 1, 2 })]
        public void UnlessT_ReturnsCorrectResults_WhenExcludingSpecificValues(int[] input, int[] expectedOutput)
        {
            // The Unless(Func<T,bool>) method is very simple and thus needs only a few very simple tests
            var result = input.Unless(item => item == 9);
            Assert.That(expectedOutput, Is.EqualTo(result));
        }

        #endregion Unless(Func<T,bool>)

        #region Unless(Func<T,T,bool>)

        [TestCase(new int[0], new int[0])]
        [TestCase(new[] { default(int) }, new[] { default(int) })]
        [TestCase(new[] { 1 }, new[] { 1 })]
        [TestCase(new[] { 1, 1 }, new[] { 1 })]
        [TestCase(new[] { 1, 1, 1 }, new[] { 1 })]
        [TestCase(new[] { 1, 2 }, new[] { 1, 2 })]
        [TestCase(new[] { 1, 1, 2 }, new[] { 1, 2 })]
        [TestCase(new[] { 1, 2, 1 }, new[] { 1, 2, 1 })]
        [TestCase(new[] { 1, 1, 2, 2, 1, 1 }, new[] { 1, 2, 1 })]
        public void UnlessTT_ReturnsCorrectResults_WhenCheckingForDuplicates(int[] input, int[] expectedOutput)
        {
            // The Unless(Func<T,T,bool>) method is a little more complicated and thus deserves a few more tests
            var result = input.Unless((last, current) => last == current);
            Assert.That(expectedOutput, Is.EqualTo(result));
        }

        #endregion Unless(Func<T,T,bool>)

        #region ToDictionary(DuplicateKeyBehavior)


        [TestCase(new int[0])]
        [TestCase(new[] { 1, 2 })]
        [TestCase(new[] { 1, 2, 3, 4 })]
        public void ToDictionary_ReturnsCorrectResults_IfThereAreNoDuplicates(int[] input)
        {
            if (input.Length % 2 != 0)
                throw new Exception("Input must contain pairs (key, value, key, value, ...)");

            var pairCount = 0;
            var kvps = new List<KeyValuePair<int, int>>();
            for (var index = 0; index < input.Length; index += 2)
            {
                var key = input[index];
                var value = input[index + 1];
                kvps.Add(new KeyValuePair<int, int>(key, value));
                pairCount++;
            }

            var dictionary = kvps.ToDictionary();

            Assert.That(dictionary, Has.Count.EqualTo(pairCount));
            for (var index = 0; index < input.Length; index += 2)
            {
                var key = input[index];
                var value = input[index + 1];

                Assert.Multiple(() =>
                {
                    Assert.That(dictionary.ContainsKey(key), Is.True);
                    Assert.That(dictionary[key], Is.EqualTo(value));
                });
            }
        }

        [Test]
        public void ToDictionary_Throws_IfDuplicateKeyBehaviorIsThrow()
        {
            var kvps = new List<KeyValuePair<int, int>>
            {
                new KeyValuePair<int, int>(1, 2),
                new KeyValuePair<int, int>(1, 3)
            };

            Assert.Throws<ArgumentException>(() => kvps.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.Throw));
        }

        [Test]
        public void ToDictionary_ReturnsFirstValue_IfDuplicateKeyBehaviorIsKeepFirst()
        {
            var kvps = new List<KeyValuePair<int, int>>
            {
                new KeyValuePair<int, int>(1, 2),
                new KeyValuePair<int, int>(1, 3)
            };

            var dictionary = kvps.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepFirst);

            Assert.That(dictionary, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey(1), Is.True);
                Assert.That(dictionary[1], Is.EqualTo(2));
            });
        }

        [Test]
        public void ToDictionary_ReturnsLastValue_IfDuplicateKeyBehaviorIsKeepLast()
        {
            var kvps = new List<KeyValuePair<int, int>>
            {
                new KeyValuePair<int, int>(1, 2),
                new KeyValuePair<int, int>(1, 3)
            };

            var dictionary = kvps.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepLast);

            Assert.That(dictionary, Has.Count.EqualTo(1));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey(1), Is.True);
                Assert.That(dictionary[1], Is.EqualTo(3));
            });
        }

        [Test]
        public void ToDictionary_UsesSuppliedEqualityComparer()
        {
            var kvps = new List<KeyValuePair<string, int>>
            {
                new KeyValuePair<string, int>("foo", 1),
                new KeyValuePair<string, int>("BAR", 2)
            };

            var dictionary = kvps.ToDictionary(IEnumerableExtensions.DuplicateKeyBehavior.KeepLast, StringComparer.OrdinalIgnoreCase);

            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("foo"), Is.True);
                Assert.That(dictionary.ContainsKey("FOO"), Is.True);
                Assert.That(dictionary.ContainsKey("bar"), Is.True);
                Assert.That(dictionary.ContainsKey("BAR"), Is.True);
                Assert.That(dictionary["foo"], Is.EqualTo(1));
                Assert.That(dictionary["FOO"], Is.EqualTo(1));
                Assert.That(dictionary["bar"], Is.EqualTo(2));
                Assert.That(dictionary["BAR"], Is.EqualTo(2));
            });
        }

        #endregion ToDictionary(DuplicateKeyBehavior)

        [Test]
        public void when_enumeration_is_empty_then_returns_empty_enumeration()
        {
            var items = new List<object>();
            var notNullItems = items.NotNull();
            Assert.That(notNullItems.Count(), Is.EqualTo(0));
        }

        [Test]
        public void when_enumeration_contains_null_only_then_returns_empty_enumeration()
        {
            var items = new List<object> { null, null };
            var notNullItems = items.NotNull();
            Assert.That(notNullItems.Count(), Is.EqualTo(0));
        }

        [Test]
        public void when_enumeration_contains_null_and_value_then_returns_enumeration_with_value()
        {
            var items = new List<object> { null, new object() };
            var notNullItems = items.NotNull();
            Assert.That(notNullItems.Count(), Is.EqualTo(1));
        }

        [Test]
        public void when_enumeration_contains_only_not_null_values_then_original_enumeration_is_returned()
        {
            var items = new List<object> { new object(), new object() };
            var notNullItems = items.NotNull();
            Assert.That(notNullItems.Count(), Is.EqualTo(2));
        }

        [Test]
        public void when_only_item_in_enumeration_throws_exception_then_empty_enumeration_is_returned()
        {
            var items = new List<object> { new object() };
            var selector = items.Select<object, object>(item => { throw new Exception(); }).Swallow();
            Assert.That(selector.Count(), Is.EqualTo(0));
        }

        [Test]
        public void when_all_items_in_enumeration_throw_exceptions_then_empty_enumeration_is_returned()
        {
            var items = new List<object> { new object(), new object() };
            var selector = items.Select<object, object>(item => { throw new Exception(); }).Swallow();
            Assert.That(selector.Count(), Is.EqualTo(0));
        }

        [Test]
        public void when_one_item_in_enumeration_throws_exception_then_other_results_are_returned()
        {
            var items = new List<bool> { false, true, false };
            var selector = items.Select(item => { if (item) throw new Exception(); return item; }).Swallow();
            Assert.That(selector.Count(), Is.EqualTo(2));
        }

        [Test]
        public void when_exception_does_not_match_exception_type_then_exception_is_not_swallowed()
        {
            var items = new List<object> { new object() };
            var selector = items.Select<object, object>(item => { throw new InvalidOperationException(); }).Swallow<object, InvalidCastException>();
            Assert.Throws<InvalidOperationException>(() => selector.ToList());
        }

        private class Foo { public bool Bar; }

        [Test]
        public void when_swallow_is_in_chain_enumeration_is_not_iterated_over_automatically()
        {
            var items = new List<Foo> { new Foo() };
            var selector = items.Select(item => { item.Bar = true; return item; }).Swallow();
            Assert.That(items[0].Bar, Is.EqualTo(false));
            selector.ToList();
            Assert.That(items[0].Bar, Is.EqualTo(true));
        }

        [Test]
        public void when_ForEachLazy_then_all_items_are_run_through_action()
        {
            var items = new[] { 1, 2, 3, 4 };
            var total = 0;
            var enumerable = items.ForEachLazy(item => total += item);
            Assert.That(total, Is.EqualTo(0));
            enumerable.ToList();
            Assert.That(total, Is.EqualTo(10));
        }

        [Test]
        public void when_ForEachNow_then_all_items_are_run_through_action_immediately()
        {
            var items = new[] { 1, 2, 3, 4 };
            var total = 0;
            items.ForEachNow(item => total += item);
            Assert.That(total, Is.EqualTo(10));
        }

        #region Flatten

        [Test]
        public void when_no_children()
        {
            var node = new Node();

            var flattened = node.Flatten(child => child.Children).ToList();

            Assert.That(flattened, Has.Count.EqualTo(1));
            Assert.That(flattened.First(), Is.SameAs(node));
        }

        [Test]
        public void when_one_child()
        {
            var node = new Node();
            var childNode = new Node();
            node.Children.Add(childNode);

            var flattened = node.Flatten(child => child.Children).ToList();

            Assert.That(flattened, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(flattened[0], Is.SameAs(node));
                Assert.That(flattened[1], Is.SameAs(childNode));
            });
        }

        [Test]
        public void when_nested_child()
        {
            var node = new Node();
            var outerChild = new Node();
            var innerChild = new Node();
            node.Children.Add(outerChild);
            outerChild.Children.Add(innerChild);

            var flattened = node.Flatten(child => child.Children).ToList();

            Assert.That(flattened, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(flattened[0], Is.SameAs(node));
                Assert.That(flattened[1], Is.SameAs(outerChild));
                Assert.That(flattened[2], Is.SameAs(innerChild));
            });
        }

        [Test]
        public void when_tree()
        {
            var node = new Node();
            var firstChild = new Node();
            var secondChild = new Node();
            node.Children.Add(firstChild);
            node.Children.Add(secondChild);

            var flattened = node.Flatten(child => child.Children).ToList();

            Assert.That(flattened, Has.Count.EqualTo(3));
            Assert.Multiple(() =>
            {
                Assert.That(flattened[0], Is.SameAs(node));
                Assert.That(flattened[2], Is.SameAs(firstChild));
                Assert.That(flattened[1], Is.SameAs(secondChild));
            });
        }

        private class Node
        {
            public readonly IList<Node> Children = new List<Node>();
        }

        #endregion Flatten
    }
}
