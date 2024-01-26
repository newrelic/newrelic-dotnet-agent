// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Core;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Text;

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
            Assert.Multiple(() =>
            {
                Assert.That(Strings.ToString(new object[] { "one" }), Is.EqualTo("one"));
                Assert.That(Strings.ToString(new object[] { "one", "two", 3 }), Is.EqualTo("one,two,3"));
            });
        }

        [Test]
        public static void TestHexParse()
        {
            Assert.That(Convert.ToInt32("0x1", 16), Is.EqualTo(1));
        }

        [Test]
        public static void validate_string_encode_with_key()
        {
            var result = Strings.Base64Encode(DATA, ENCODE_KEY);
            var expected = "PxQGQldBFBFnAQM2RVBZFQVSQloNCEwwYC97DVRUUVFHCktOcwRSAQtYZAQUFVFQVTcEEEFdUhJLVlNHIwUADUUMTCtWXltAXgRNCF4PEU5UGAdNVk0JAQNIVVNq";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public static void validate_string_encode_crossprocessid_with_key()
        {
            var result = Strings.Base64Encode("269975#19205", ENCODE_KEY);
            var expected = "VgAOWFFWGwIJVlFX";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public static void validate_string_decode_with_key()
        {
            var result = Strings.Base64Decode("PxQGQldBFBFnAQM2RVBZFQVSQloNCEwwYC97DVRUUVFHCktOcwRSAQtYZAQUFVFQVTcEEEFdUhJLVlNHIwUADUUMTCtWXltAXgRNCF4PEU5UGAdNVk0JAQNIVVNq", ENCODE_KEY);
            Assert.That(result, Is.EqualTo(DATA));
        }

        [Test]
        public static void validate_string_encode_without_key()
        {
            var result = Strings.Base64Encode(DATA);
            var expected = "WyIxIzEiLCJXZWJUcmFuc2FjdGlvbi9SUE1Db2xsZWN0b3IvQmVhY29uU2VydmljZVNlcnZsZXQvZ2V0QWNjb3VudEluZm9ybWF0aW9uIiwwLjAsMC4xMjMsNDFd";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public static void validate_string_decode_without_key()
        {
            var result = Strings.Base64Decode("WyIxIzEiLCJXZWJUcmFuc2FjdGlvbi9SUE1Db2xsZWN0b3IvQmVhY29uU2VydmljZVNlcnZsZXQvZ2V0QWNjb3VudEluZm9ybWF0aW9uIiwwLjAsMC4xMjMsNDFd");
            Assert.That(result, Is.EqualTo(DATA));
        }


        [Test]
        public static void validate_string_decode_throws_with_invalid_encoded_value()
        {
            Assert.Throws<FormatException>(() => Strings.Base64Decode("1234#134634643"));
        }

        [TestCaseSource(nameof(ConvertBytesToStringTestData))]
        public void when_byte_array_is_decoded_into_string_one_byte_at_a_time_then_result_string_is_correct(Encoding encoding, string content)
        {
            var bytes = encoding.GetBytes(content);
            var decoder = encoding.GetDecoder();
            var result = string.Empty;
            foreach (var @byte in bytes)
            {
                result += Strings.GetStringBufferFromBytes(decoder, new byte[] { @byte }, 0, 1);
            }
            Assert.That(result, Is.EqualTo(content));
        }

        [TestCase("commonName", ExpectedResult = "commonName")]
        [TestCase("commonName.log", ExpectedResult = "commonName.log")]
        [TestCase("name with spaces", ExpectedResult = "name with spaces")]
        [TestCase("name\"with\\|invalidchars_", ExpectedResult = "name_with__invalidchars_")]
        public string SafeFileName_Tests(string inputName)
        {
            return Strings.SafeFileName(inputName);
        }

        [TestCase("[{\"high_security\":false}]", ExpectedResult = "[{\"high_security\":false}]")]
        [TestCase("https://collector.newrelic.com/agent_listener/invoke_raw_method?method=preconnect&license_key=abcdeabcdeabcdeabcdeabcdeabcdeabcdeabcde&marshal_format=json&protocol_version=17",
            ExpectedResult = "https://collector.newrelic.com/agent_listener/invoke_raw_method?method=preconnect&license_key=abcdeabc********************************&marshal_format=json&protocol_version=17")]
        [TestCase("https://collector.newrelic.com/agent_listener/invoke_raw_method?method=preconnect&license_key=shortLicenseKey&marshal_format=json&protocol_version=17",
            ExpectedResult = "https://collector.newrelic.com/agent_listener/invoke_raw_method?method=preconnect&license_key=***************&marshal_format=json&protocol_version=17")]
        public string ObfuscateLicenseKeyForAuditLog(string inputText)
        {
            return Strings.ObfuscateLicenseKeyInAuditLog(inputText, "license_key");
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
