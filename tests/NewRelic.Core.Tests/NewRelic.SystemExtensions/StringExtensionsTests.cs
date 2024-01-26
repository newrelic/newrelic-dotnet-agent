// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NewRelic.SystemExtensions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace NewRelic.Core.Tests.NewRelic.SystemExtensions
{
    [TestFixture]
    public class StringExtensionsTests
    {
        [Test]
        public void when_maxLength_is_less_than_0_then_throws_exception()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => "foo".TruncateUnicodeStringByLength(-1));
        }

        [Test]
        public void when_null_then_throws_exception()
        {
            Assert.Throws<ArgumentNullException>(() => (null as string).TruncateUnicodeStringByLength(0));
        }

        [TestCase("foo", 4, "foo")]
        [TestCase("foo", 3, "foo")]
        [TestCase("foo", 2, "fo")]
        [TestCase("foo", 0, "")]
        [TestCase("€€€", 3, "€€€")]
        [TestCase("€€€", 2, "€€")]
        [TestCase("a\u0304\u0308bc\u0327", 3, "a\u0304\u0308bc\u0327")]
        public void TruncationByLength(string inputString, int maxLength, string expectedResult)
        {
            var actualResult = inputString.TruncateUnicodeStringByLength(maxLength);

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("abcde♥", (uint)10, "abcde♥")] //"abcde♥" has 8 bytes
        [TestCase("abcde♥", (uint)8, "abcde♥")]  //"abcde♥" has 8 bytes
        [TestCase("abcde♥", (uint)7, "abcde")]   //"abcde♥" has 8 bytes
        [TestCase("abcგთde♥", (uint)8, "abcგ")]  // "abcგთde♥" has 14 bytes
        [TestCase(null, (uint)8, null)]
        [TestCase("", (uint)8, "")]
        [TestCase("asdf", (uint)0, "")]
        [TestCase("asdf", (uint)1, "a")]
        [TestCase("♥asdf", (uint)1, "")]
        public void TruncationByBytes(string inputString, uint maxBytes, string expectedResult)
        {
            var actualResult = inputString.TruncateUnicodeStringByBytes(maxBytes);
            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase(null, false, new string[] { })]
        [TestCase(null, false, new[] { "baz" })]
        [TestCase("foo", false, new string[] { })]
        [TestCase("foo", false, new[] { "bar" })]
        [TestCase("foo", true, new[] { "foo" })]
        [TestCase("foo", true, new[] { "FOO" })]
        [TestCase("foo", true, new[] { "foo", "baz" })]
        [TestCase("foobar", true, new[] { "foo" })]
        [TestCase("foobar", true, new[] { "FOO" })]
        [TestCase("foobar", true, new[] { "foo", "baz" })]
        public void ContainsAny_ReturnsTrue_IfSourceStringContainsAnyOfTargetStrings_WhileIgnoringCase(string source, bool expectedResult, params string[] searchTargets)
        {
            var result = source.ContainsAny(searchTargets, StringComparison.InvariantCultureIgnoreCase);

            Assert.That(result, Is.EqualTo(expectedResult));
        }

        [TestCase(null, false, new string[] { })]
        [TestCase(null, false, new[] { "baz" })]
        [TestCase("foo", false, new string[] { })]
        [TestCase("foo", false, new[] { "bar" })]
        [TestCase("foo", true, new[] { "foo" })]
        [TestCase("foo", false, new[] { "FOO" })]
        [TestCase("foo", true, new[] { "foo", "baz" })]
        [TestCase("foobar", true, new[] { "foo" })]
        [TestCase("foobar", false, new[] { "FOO" })]
        [TestCase("foobar", true, new[] { "foo", "baz" })]
        public void ContainsAny_ReturnsTrue_IfSourceStringContainsAnyOfTargetStrings_WhileRespectingCase(string source, bool expectedResult, params string[] searchTargets)
        {
            Assert.That(source.ContainsAny(searchTargets, StringComparison.InvariantCulture), Is.EqualTo(expectedResult));
        }

        [Test]
        public void ContainsAny_ReturnsFalse_IfSourceStringIsNull()
        {
            string source = null;
            Assert.That(source.ContainsAny(new [] { "foo"}), Is.False);
        }

        [Test]
        public void ContainsAny_ReturnsFalse_IfSearchTargetsIsNull()
        {
            string source = "foo";
            Assert.That(source.ContainsAny(null), Is.False);
        }

        [TestCase("foo bar zip zap", 'z', "foo bar ")]
        [TestCase("foo-bar-baz", '-', "foo")]
        [TestCase("foo☃bar☃baz", '☃', "foo")]
        [TestCase("☃-☃☃-☃☃☃", '-', "☃")]
        [TestCase("foo€bar€baz", '€', "foo")]
        [TestCase("€-€€-€€€", '-', "€")]
        [TestCase("http://www.google.com?query=blah", '?', "http://www.google.com")]
        [TestCase("Some Random String", 'z', "Some Random String")]
        public void TrimAfterAChar(string source, char token, string expectedResult)
        {
            Assert.That(source.TrimAfterAChar(token), Is.EqualTo(expectedResult));
        }

        [Test]
        public void TrimAfterAChar_Throws_IfSourceIsNull()
        {
            string source = null;
            Assert.Throws<ArgumentNullException>(() => source.TrimAfterAChar('x'));
        }

        [TestCase("abc123", 'x', 1, "abc123")]
        [TestCase("abc123", '2', 1, "abc123")]
        [TestCase("abc123", '3', 0, "abc123")]
        [TestCase("abc123", '3', -1, "abc123")]
        [TestCase("abc123", '3', 1, "abc12")]
        [TestCase("abc123", '3', 5, "abc12")]
        [TestCase("abc333", '3', 1, "abc33")]
        [TestCase("abc333", '3', 3, "abc")]
        public void TrimEnd(string source, char trimChar, int maxCharactersToTrim, string expectedResult)
        {
            if (source == null)
                throw new ArgumentNullException("source");

            var actualResult = source.TrimEnd(trimChar, maxCharactersToTrim);

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("foo", "bar", "barfoo")]
        [TestCase("barfoo", "bar", "barfoo")]
        [TestCase("arfoo", "bar", "bararfoo")]
        [TestCase("foo", null, "foo")]
        public void EnsureLeading(string source, string leading, string expectedResult)
        {
            var actualResult = source.EnsureLeading(leading);

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("foo", "bar", "foobar")]
        [TestCase("foobar", "bar", "foobar")]
        [TestCase("fooba", "bar", "foobabar")]
        [TestCase("foo", null, "foo")]
        public void EnsureTrailing(string source, string trailing, string expectedResult)
        {
            var actualResult = source.EnsureTrailing(trailing);

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("foo", "Foo")]
        [TestCase("Bar", "Bar")]
        [TestCase("Foo", "Foo")]
        [TestCase("BAR", "BAR")]
        [TestCase("the quick brown fox", "The quick brown fox")]
        [TestCase("2251", "2251")]
        [TestCase(" ", " ")]
        public void CapitalizeWord(string word, string expectedResult)
        {
            var actualResult = word.CapitalizeWord();

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("the quick brown fox", ' ', false, "The Quick Brown Fox")]
        [TestCase("the quick brown fox", ' ', true, "TheQuickBrownFox")]
        [TestCase("the_quick_brown_fox", '_', false, "The_Quick_Brown_Fox")]
        [TestCase("the_quick_brown_fox", '_', true, "TheQuickBrownFox")]
        [TestCase("_foo", '_', true, "Foo")]
        [TestCase("_foo", '_', false, "_Foo")]
        [TestCase("bar_", '_', true, "Bar")]
        [TestCase("bar_", '_', false, "Bar_")]
        [TestCase("_foo_bar_", '_', true, "FooBar")]
        [TestCase("_foo_bar_", '_', false, "_Foo_Bar_")]

        public void CapitalizeEachWord(string sentence, char separator, bool removeSeparator, string expectedResult)
        {
            var actualResult = sentence.CapitalizeEachWord(separator, removeSeparator);

            Assert.That(actualResult, Is.EqualTo(expectedResult));
        }

        [TestCase("foo bar baz")]
        [TestCase(null)]
        public void SizeBytes(string testString)
        {
            var expectedSizeBytes = testString == null ? 0 : Encoding.UTF8.GetByteCount(testString);

            Assert.That(testString.SizeBytes(), Is.EqualTo(expectedSizeBytes));
        }
    }
}
