// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class StringsTest
    {
        internal const string ENCODE_KEY = "d67afc830dab717fd163bfcb0b8b88423e9a1a3b";
        internal const string DATA = "[\"1#1\",\"WebTransaction/RPMCollector/BeaconServiceServlet/getAccountInformation\",0.0,0.123,41]";

        [Test]
        public static void TestToString()
        {
            Assert.AreEqual("one", Strings.ToString(new object[] { "one" }));
            Assert.AreEqual("one,two,3", Strings.ToString(new object[] { "one", "two", 3 }));
        }

        [Test]
        public static void TestSafeMethodName()
        {
            Assert.AreEqual("dude", Strings.SafeMethodName("dude"));
            Assert.AreEqual("Obfuscated", Strings.SafeMethodName(new string(new char[] { '\u0003' })));
            Assert.AreEqual("Obfuscated", Strings.SafeMethodName(new string(new char[] { '\u0020' })));
            Assert.AreEqual("!", Strings.SafeMethodName(new string(new char[] { '\u0021' })));
        }

        [Test]
        public static void TestFixDatabaseObjectNameWithBrackets()
        {
            Assert.AreEqual("dude", Strings.FixDatabaseObjectName("[dude]"));
        }

        [Test]
        public static void TestUnquoteDouble()
        {
            Assert.AreEqual("dude", Strings.FixDatabaseObjectName("\"dude\""));
        }

        [Test]
        public static void TestUnquoteSingle()
        {
            Assert.AreEqual("dude", Strings.FixDatabaseObjectName("'dude'"));
        }

        [Test]
        public static void TestUnquoteTick()
        {
            Assert.AreEqual("dude", Strings.FixDatabaseObjectName("`dude`"));
        }

        [Test]
        public static void TestHexParse()
        {
            Assert.AreEqual(1, Convert.ToInt32("0x1", 16));
        }

        [Test]
        public static void TestToRubyName()
        {
            Assert.AreEqual("test_man", Strings.ToRubyName("testMan"));
            Assert.AreEqual("transaction_tracer", Strings.ToRubyName("transactionTracer"));
        }

        [Test]
        public static void validate_string_encode_with_key()
        {
            var result = Strings.Base64Encode(DATA, ENCODE_KEY);
            var expected = "PxQGQldBFBFnAQM2RVBZFQVSQloNCEwwYC97DVRUUVFHCktOcwRSAQtYZAQUFVFQVTcEEEFdUhJLVlNHIwUADUUMTCtWXltAXgRNCF4PEU5UGAdNVk0JAQNIVVNq";
            Assert.AreEqual(expected, result);
        }

        [Test]
        public static void validate_string_encode_crossprocessid_with_key()
        {
            var result = Strings.Base64Encode("269975#19205", ENCODE_KEY);
            var expected = "VgAOWFFWGwIJVlFX";
            Assert.AreEqual(expected, result);
        }

        [Test]
        public static void validate_string_decode_with_key()
        {
            var result = Strings.Base64Decode("PxQGQldBFBFnAQM2RVBZFQVSQloNCEwwYC97DVRUUVFHCktOcwRSAQtYZAQUFVFQVTcEEEFdUhJLVlNHIwUADUUMTCtWXltAXgRNCF4PEU5UGAdNVk0JAQNIVVNq", ENCODE_KEY);
            Assert.AreEqual(DATA, result);
        }

        [Test]
        public static void validate_string_encode_without_key()
        {
            var result = Strings.Base64Encode(DATA);
            var expected = "WyIxIzEiLCJXZWJUcmFuc2FjdGlvbi9SUE1Db2xsZWN0b3IvQmVhY29uU2VydmljZVNlcnZsZXQvZ2V0QWNjb3VudEluZm9ybWF0aW9uIiwwLjAsMC4xMjMsNDFd";
            Assert.AreEqual(expected, result);
        }

        [Test]
        public static void validate_string_decode_without_key()
        {
            var result = Strings.Base64Decode("WyIxIzEiLCJXZWJUcmFuc2FjdGlvbi9SUE1Db2xsZWN0b3IvQmVhY29uU2VydmljZVNlcnZsZXQvZ2V0QWNjb3VudEluZm9ybWF0aW9uIiwwLjAsMC4xMjMsNDFd");
            Assert.AreEqual(DATA, result);
        }


        [Test]
        public static void validate_string_decode_throws_with_invalid_encoded_value()
        {
            Assert.Throws<FormatException>(() => Strings.Base64Decode("1234#134634643"));
        }

        [Test]
        public static void validate_cleanuri_is_stringempty_when_uri_is_null()
        {
            Uri uri = null;
            var result = Strings.CleanUri(uri);
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public static void validate_cleanuri_returns_original_uri_for_relative_uris()
        {
            Uri uri = new Uri("/relative/uri", UriKind.Relative);
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("/relative/uri", result);
        }

        [Test]
        public static void validate_cleanuri_keeps_conventional_port_80()
        {
            var uri = new Uri("http://www.example.com:80/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:80/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_keeps_conventional_port_443()
        {
            var uri = new Uri("http://www.example.com:443/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_strips_userpassword()
        {
            var uri = new Uri("http://username:password@example.com:443/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_removes_querystring()
        {
            var uri = new Uri("http://www.example.com:80/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:80/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_removes_querystring_and_conventional_port_443()
        {
            var uri = new Uri("http://www.example.com:443/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:443/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_leaves_unconventional_ports()
        {
            var uri = new Uri("http://www.example.com:8080/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:8080/dir/", result);
        }

        [Test]
        public static void validate_cleanuri_leaves_unconventional_ports_removes_querystring()
        {
            var uri = new Uri("http://www.example.com:8080/dir/?query=test");
            var result = Strings.CleanUri(uri);
            Assert.AreEqual("http://www.example.com:8080/dir/", result);
        }

        [Test]
        public static void validate_parse_exact_on_json()
        {
            var format = "[\"{0}\",\"{1}\",{2},{3},{4}]";
            var data = string.Format(
                              format,
                              "123",
                              "MyTransaction",
                              1000,
                              2000,
                              3000);
            var result = Strings.ParseExact(data, format);
            Assert.AreEqual(result[0], "123");
        }

        [Test]
        public static void validate_parse_exact_on_bad_json_fails()
        {
            var ex = Assert.Throws<ArgumentException>(() => Strings.ParseExact("", "[\"{0}\",\"{1}\",{2},{3}]"));
            Assert.That(ex.Message, Is.EqualTo("Format not compatible with value."));
        }

        [TestCaseSource("ConvertBytesToStringTestData")]
        public void when_byte_array_is_decoded_into_string_one_byte_at_a_time_then_result_string_is_correct(Encoding encoding, string content)
        {
            var bytes = encoding.GetBytes(content);
            var decoder = encoding.GetDecoder();
            var result = string.Empty;
            foreach (var @byte in bytes)
            {
                result += Strings.GetStringBufferFromBytes(decoder, new byte[] { @byte }, 0, 1);
            }
            Assert.AreEqual(content, result);
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
            var actual = Strings.CleanUri(uri);
            Assert.AreEqual(expected, actual);
        }

        private static IEnumerable<object[]> ConvertBytesToStringTestData()
        {
            var encodings = new Encoding[] { Encoding.Unicode, Encoding.UTF8, Encoding.ASCII };
            foreach (var encoding in encodings)
            {
                yield return new object[] { encoding, "abcdefghijklmnop" }; // ascii
            }

            encodings = new Encoding[] { Encoding.Unicode, Encoding.UTF8 };
            foreach (var encoding in encodings)
            {
                yield return new object[] { encoding, "\u000000" };
                yield return new object[] { encoding, "\u00007F" };
                yield return new object[] { encoding, "\u000080" };
                yield return new object[] { encoding, "\u00009F" };
                yield return new object[] { encoding, "\u0000A0" };
                yield return new object[] { encoding, "\u0003FF" };
                yield return new object[] { encoding, "\u000400" };
                yield return new object[] { encoding, "\u0007FF" };
                yield return new object[] { encoding, "\u000800" };
                yield return new object[] { encoding, "\u003FFF" };
                yield return new object[] { encoding, "\u004000" };
                yield return new object[] { encoding, "\u00FFFF" };
                yield return new object[] { encoding, "\u010000" };
                yield return new object[] { encoding, "\u03FFFF" };
                yield return new object[] { encoding, "\u040000" };
                yield return new object[] { encoding, "\u10FFFF" };
                yield return new object[] { encoding, "\uD800\udc05" }; // surrogate characters
                yield return new object[] { encoding, "\u000000\u00007F\u000080\u00009F\u0000A0\u0000A0\u0003FF\u000400\u0007FF\u000800\u003FFF\u004000\u00FFFF\u010000\u03FFFF\u040000\u10FFFF\uD800\udc05" }; // mixed
                yield return new object[] { encoding, "AB YZ 19 \uD800\udc05" }; // just to make sure
            }
        }

    }
}
