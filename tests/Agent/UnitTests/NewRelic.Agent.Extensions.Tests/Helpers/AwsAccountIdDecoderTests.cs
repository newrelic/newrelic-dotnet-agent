// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Extensions.AwsSdk;
using NUnit.Framework;
using Telerik.JustMock;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    internal class AwsAccountIdDecoderTests
    {
        [Test]
        public void GetAccountId_ValidAwsAccessKeyId_ReturnsExpectedAccountId()
        {
            // Arrange
            string awsAccessKeyId = "AKIAIOSFODNN7EXAMPLE"; // not a real AWS access key!
            string expectedAccountId = "581039954779"; 

            // Act
            string actualAccountId = AwsAccountIdDecoder.GetAccountId(awsAccessKeyId);

            // Assert
            Assert.That(expectedAccountId, Is.EqualTo(actualAccountId));
        }

        [Test]
        public void GetAccountId_NullOrEmptyAwsAccessKeyId_ThrowsArgumentNullException()
        {
            // Arrange
            string awsAccessKeyId = null;

            // Act & Assert
            var ex = Assert.Throws<ArgumentNullException>(() => AwsAccountIdDecoder.GetAccountId(awsAccessKeyId));
            Assert.That(ex.ParamName, Is.EqualTo("awsAccessKeyId"));
        }

        [Test]
        public void GetAccountId_ShortAwsAccessKeyId_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            string awsAccessKeyId = "AKIAIOSFODN";

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AwsAccountIdDecoder.GetAccountId(awsAccessKeyId));
            Assert.That(ex.ParamName, Is.EqualTo("awsAccessKeyId"));
        }

        [Test]
        public void Base32Decode_ShortString_ThrowsArgumentException()
        {
            // Arrange
            string shortString = "shortstr";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => AwsAccountIdDecoder.Base32Decode(shortString));
            Assert.That(ex.ParamName, Is.EqualTo("src"));
        }

        [Test]
        public void Base32Decode_InvalidCharacters_ThrowsArgumentOutOfRangeException()
        {
            // Arrange
            string invalidBase32String = "someBogusbase32string";

            // Act & Assert
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => AwsAccountIdDecoder.Base32Decode(invalidBase32String));
            Assert.That(ex.ParamName, Is.EqualTo("src"));
        }

        [Test]
        public void Base32Decode_NullOrEmptyString_ThrowsArgumentNullException()
        {
            // Arrange
            string nullString = null;
            string emptyString = string.Empty;

            // Act & Assert
            var exNull = Assert.Throws<ArgumentNullException>(() => AwsAccountIdDecoder.Base32Decode(nullString));
            Assert.That(exNull.ParamName, Is.EqualTo("src"));

            var exEmpty = Assert.Throws<ArgumentNullException>(() => AwsAccountIdDecoder.Base32Decode(emptyString));
            Assert.That(exEmpty.ParamName, Is.EqualTo("src"));
        }

        [Test]
        public void Base32Decode_ValidBase32String_ReturnsDecodedLong()
        {
            // Arrange
            string validBase32String = "iosfodnn7example"; // Example valid Base32 string (10 characters)
            long expectedDecodedValue = 74373114211833L;

            // Act
            long decodedValue = AwsAccountIdDecoder.Base32Decode(validBase32String);

            // Assert
            Assert.That(decodedValue, Is.EqualTo(expectedDecodedValue)); // Adjust expected value based on actual decoding logic
        }

    }
}
