// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Specialized;
using NewRelic.SystemExtensions.Collections;


namespace NewRelic.SystemExtensions.UnitTests.Collections
{
    public class NameValueCollectionExtensions
    {
        [Test]
        public void ToDictionary_CreatesCorrectDictionary()
        {
            var collection = new NameValueCollection
            {
                {"fruit", "apple"},
                {"dessert", "pie"}
            };

            var dictionary = collection.ToDictionary();

            ClassicAssert.AreEqual(2, dictionary.Count);
            Assert.That(dictionary.ContainsKey("fruit"));
            ClassicAssert.AreEqual("apple", dictionary["fruit"]);
            Assert.That(dictionary.ContainsKey("dessert"));
            ClassicAssert.AreEqual("pie", dictionary["dessert"]);
        }

        [Test]
        public void ToDictionary_SkipsNullKeys()
        {
            var collection = new NameValueCollection
            {
                { "fruit", "apple" },
                { null, "42" },
                { "dessert", "pie" },
            };

            var dictionary = collection.ToDictionary();

            ClassicAssert.AreEqual(2, dictionary.Count);
            Assert.That(dictionary.ContainsKey("fruit"));
            ClassicAssert.AreEqual("apple", dictionary["fruit"]);
            Assert.That(dictionary.ContainsKey("dessert"));
            ClassicAssert.AreEqual("pie", dictionary["dessert"]);
        }

        [Test]
        public void ToDictionary_IsCaseInsensitiveByDefault()
        {
            var collection = new NameValueCollection
            {
                {"fruit", "apple"},
                {"DESSERT", "pie"}
            };

            var dictionary = collection.ToDictionary();

            ClassicAssert.AreEqual(2, dictionary.Count);
            Assert.That(dictionary.ContainsKey("fruit"));
            Assert.That(dictionary.ContainsKey("FRUIT"));
            ClassicAssert.AreEqual("apple", dictionary["fruit"]);
            ClassicAssert.AreEqual("apple", dictionary["FRUIT"]);
            Assert.That(dictionary.ContainsKey("dessert"));
            Assert.That(dictionary.ContainsKey("DESSERT"));
            ClassicAssert.AreEqual("pie", dictionary["dessert"]);
            ClassicAssert.AreEqual("pie", dictionary["DESSERT"]);
        }

        [Test]
        public void ToDictionary_UsesSuppliedEqualityComparer()
        {
            var collection = new NameValueCollection
            {
                {"fruit", "apple"},
                {"DESSERT", "pie"}
            };

            var dictionary = collection.ToDictionary(StringComparer.CurrentCulture);

            ClassicAssert.AreEqual(2, dictionary.Count);
            Assert.That(dictionary.ContainsKey("fruit"));
            ClassicAssert.AreEqual("apple", dictionary["fruit"]);
            Assert.That(dictionary.ContainsKey("DESSERT"));
            ClassicAssert.AreEqual("pie", dictionary["DESSERT"]);

            ClassicAssert.False(dictionary.ContainsKey("FRUIT"));
            ClassicAssert.False(dictionary.ContainsKey("dessert"));
        }
    }
}
