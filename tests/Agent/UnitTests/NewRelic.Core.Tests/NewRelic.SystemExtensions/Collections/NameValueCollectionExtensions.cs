/*
* Copyright 2020 New Relic Corporation. All rights reserved.
* SPDX-License-Identifier: Apache-2.0
*/
using System;
using System.Collections.Specialized;
using NewRelic.SystemExtensions.Collections;
using NUnit.Framework;


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

            Assert.AreEqual(2, dictionary.Count);
            Assert.True(dictionary.ContainsKey("fruit"));
            Assert.AreEqual("apple", dictionary["fruit"]);
            Assert.True(dictionary.ContainsKey("dessert"));
            Assert.AreEqual("pie", dictionary["dessert"]);
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

            Assert.AreEqual(2, dictionary.Count);
            Assert.True(dictionary.ContainsKey("fruit"));
            Assert.AreEqual("apple", dictionary["fruit"]);
            Assert.True(dictionary.ContainsKey("dessert"));
            Assert.AreEqual("pie", dictionary["dessert"]);
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

            Assert.AreEqual(2, dictionary.Count);
            Assert.True(dictionary.ContainsKey("fruit"));
            Assert.True(dictionary.ContainsKey("FRUIT"));
            Assert.AreEqual("apple", dictionary["fruit"]);
            Assert.AreEqual("apple", dictionary["FRUIT"]);
            Assert.True(dictionary.ContainsKey("dessert"));
            Assert.True(dictionary.ContainsKey("DESSERT"));
            Assert.AreEqual("pie", dictionary["dessert"]);
            Assert.AreEqual("pie", dictionary["DESSERT"]);
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

            Assert.AreEqual(2, dictionary.Count);
            Assert.True(dictionary.ContainsKey("fruit"));
            Assert.AreEqual("apple", dictionary["fruit"]);
            Assert.True(dictionary.ContainsKey("DESSERT"));
            Assert.AreEqual("pie", dictionary["DESSERT"]);

            Assert.False(dictionary.ContainsKey("FRUIT"));
            Assert.False(dictionary.ContainsKey("dessert"));
        }
    }
}
