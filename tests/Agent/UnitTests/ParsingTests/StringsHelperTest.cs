// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Parsing
{
    [TestFixture]
    public class StringsHelperTest
    {
        [Test]
        public void DoubleBracket()
        {
            ClassicAssert.AreEqual("dude", StringsHelper.RemoveBracketsQuotesParenthesis("[[dude]]"));
        }

        [Test]
        public static void TestFixDatabaseObjectNameWithBrackets()
        {
            ClassicAssert.AreEqual("dude", StringsHelper.FixDatabaseObjectName("[dude]"));
        }

        [Test]
        public static void TestUnquoteDouble()
        {
            ClassicAssert.AreEqual("dude", StringsHelper.FixDatabaseObjectName("\"dude\""));
        }

        [Test]
        public static void TestUnquoteSingle()
        {
            ClassicAssert.AreEqual("dude", StringsHelper.FixDatabaseObjectName("'dude'"));
        }

        [Test]
        public static void TestUnquoteTick()
        {
            ClassicAssert.AreEqual("dude", StringsHelper.FixDatabaseObjectName("`dude`"));
        }

        [Test]
        public static void validate_cleanuri_is_stringempty_when_uri_is_null()
        {
            Uri uri = null;
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual(string.Empty, result);
        }

        [Test]
        public static void validate_cleanuri_returns_original_uri_for_relative_uris()
        {
            Uri uri = new Uri("/relative/uri", UriKind.Relative);
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("/relative/uri", result);
        }

        [Test]
        public static void validate_cleanuri_returns_original_uri_for_relative_uris_and_strips_querystring()
        {
            Uri uri = new Uri("/relative/uri?dude=666", UriKind.Relative);
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("/relative/uri", result);
        }

        [Test]
        public static void validate_cleanuri_keeps_conventional_port_80()
        {
            var uri = new Uri("http://www.example.com:80/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:80/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_keeps_conventional_port_443()
        {
            var uri = new Uri("http://www.example.com:443/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_strips_userpassword()
        {
            var uri = new Uri("http://username:password@example.com:443/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_removes_querystring()
        {
            var uri = new Uri("http://www.example.com:80/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:80/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_removes_querystring_and_conventional_port_443()
        {
            var uri = new Uri("http://www.example.com:443/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_leaves_unconventional_ports()
        {
            var uri = new Uri("http://www.example.com:8080/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:8080/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_leaves_unconventional_ports_removes_querystring()
        {
            var uri = new Uri("http://www.example.com:8080/dir/?query=test");
            var result = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual("http://www.example.com:8080/dir/", result);
        }

        [TestCase("http://testsite.com/abc", "http://testsite.com/abc")]
        [TestCase("http://testsite.com?user=bob", "http://testsite.com")]
        [TestCase("http://testsite.com/?user=bob", "http://testsite.com/")]
        [TestCase("http://testsite.com?user=bob&pwd=yoasdl~", "http://testsite.com")]
        [TestCase("http://testsite.com%3F", "http://testsite.com%3F")]
        [TestCase("http://testsite.com?auth=http://verifyme.com?testing=blah", "http://testsite.com")]
        [TestCase("http://testsite.com?auth=http://verifyme.com%3Fxlxl=x1", "http://testsite.com")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void validate_CleanUri_String_Version(string uri, string expected)
        {
            var actual = StringsHelper.CleanUri(uri);
            ClassicAssert.AreEqual(expected, actual);
        }
    }
}
