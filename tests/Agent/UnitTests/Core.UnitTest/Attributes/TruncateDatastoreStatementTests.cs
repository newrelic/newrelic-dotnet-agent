// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;
using NUnit.Framework;
using NewRelic.Testing.Assertions;

namespace NewRelic.Agent.Core.Attributes.Tests
{
    [TestFixture]
    public class TruncateDatastoreStatementTests
    {
        [Test]
        public void Returns_null_when_input_is_null()
        {
            var result = AttributeDefinitionBuilder.TruncateDatastoreStatement(null, 10);

            Assert.That(result, Is.Null);
        }

        [Test]
        public void Returns_input_when_within_byte_limit()
        {
            const string input = "select * from table";
            var max = Encoding.UTF8.GetByteCount(input) + 5; // comfortably larger

            var result = AttributeDefinitionBuilder.TruncateDatastoreStatement(input, max);

            NrAssert.Multiple(
                () => Assert.That(result, Is.EqualTo(input)),
                () => Assert.That(Encoding.UTF8.GetByteCount(result), Is.LessThanOrEqualTo(max))
            );
        }

        [Test]
        public void Truncates_and_appends_ellipsis_when_exceeding_limit()
        {
            var input = new string('x', 100);
            var max = 20; // force truncation

            var result = AttributeDefinitionBuilder.TruncateDatastoreStatement(input, max);

            NrAssert.Multiple(
                () => Assert.That(result, Does.EndWith("...")),
                () => Assert.That(Encoding.UTF8.GetByteCount(result), Is.LessThanOrEqualTo(max))
            );
        }

        [Test]
        public void Truncates_on_utf8_character_boundary_for_multibyte_characters()
        {
            // "😀" (U+1F600) encodes to 4 bytes in UTF-8. We choose a limit that would
            // land within the emoji if not handled correctly, then ensure we backtrack
            // to the start of the character.
            const string input = "abc😀def"; // bytes: 3 + 4 + 3 = 10
            var ellipsisBytes = Encoding.UTF8.GetByteCount("...");

            // Choose max such that offset (max - ellipsisBytes) falls on a continuation byte of the emoji
            // bytes index layout: a(0) b(1) c(2) 😀(3..6) d(7) e(8) f(9)
            var max = 4 + ellipsisBytes; // offset=4 -> in the middle of the emoji

            var result = AttributeDefinitionBuilder.TruncateDatastoreStatement(input, max);

            NrAssert.Multiple(
                () => Assert.That(result, Is.EqualTo("abc...")),
                () => Assert.That(Encoding.UTF8.GetByteCount(result), Is.LessThanOrEqualTo(max))
            );
        }

        [Test]
        public void Returns_input_when_exactly_at_byte_limit()
        {
            const string input = "abc😀def";
            var inputBytes = Encoding.UTF8.GetByteCount(input);

            var result = AttributeDefinitionBuilder.TruncateDatastoreStatement(input, inputBytes);

            NrAssert.Multiple(
                () => Assert.That(result, Is.EqualTo(input)),
                () => Assert.That(Encoding.UTF8.GetByteCount(result), Is.EqualTo(inputBytes))
            );
        }
    }
}
