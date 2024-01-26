// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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

            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("fruit"), Is.True);
                Assert.That(dictionary["fruit"], Is.EqualTo("apple"));
                Assert.That(dictionary.ContainsKey("dessert"), Is.True);
                Assert.That(dictionary["dessert"], Is.EqualTo("pie"));
            });
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

            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("fruit"), Is.True);
                Assert.That(dictionary["fruit"], Is.EqualTo("apple"));
                Assert.That(dictionary.ContainsKey("dessert"), Is.True);
                Assert.That(dictionary["dessert"], Is.EqualTo("pie"));
            });
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

            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("fruit"), Is.True);
                Assert.That(dictionary.ContainsKey("FRUIT"), Is.True);
                Assert.That(dictionary["fruit"], Is.EqualTo("apple"));
                Assert.That(dictionary["FRUIT"], Is.EqualTo("apple"));
                Assert.That(dictionary.ContainsKey("dessert"), Is.True);
                Assert.That(dictionary.ContainsKey("DESSERT"), Is.True);
                Assert.That(dictionary["dessert"], Is.EqualTo("pie"));
                Assert.That(dictionary["DESSERT"], Is.EqualTo("pie"));
            });
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

            Assert.That(dictionary, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("fruit"), Is.True);
                Assert.That(dictionary["fruit"], Is.EqualTo("apple"));
                Assert.That(dictionary.ContainsKey("DESSERT"), Is.True);
                Assert.That(dictionary["DESSERT"], Is.EqualTo("pie"));
            });

            Assert.Multiple(() =>
            {
                Assert.That(dictionary.ContainsKey("FRUIT"), Is.False);
                Assert.That(dictionary.ContainsKey("dessert"), Is.False);
            });
        }
    }
}
