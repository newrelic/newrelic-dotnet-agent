// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace NewRelic.Agent.Core.UnitTest.Utilities
{
    [TestFixture]
    public class HexStringConverterTests
    {
        [Test]
        public void FromHexString_ValidHexString_ReturnsByteArray()
        {
            // Arrange
            var hexString = "4a6f686e446f65"; // "JohnDoe" in hex
            var expected = new byte[] { 0x4a, 0x6f, 0x68, 0x6e, 0x44, 0x6f, 0x65 };

            // Act
            var result = hexString.AsSpan().FromHexString();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FromHexString_InvalidHexCharacter_ThrowsArgumentException()
        {
            // Arrange
            var invalidHexString = "4a6f6g";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => invalidHexString.AsSpan().FromHexString());
            Assert.That(ex.Message, Does.Contain("Invalid hexadecimal character"));
        }

        [Test]
        public void FromHexString_OddLengthHexString_ThrowsArgumentException()
        {
            // Arrange
            var oddLengthHexString = "4a6f6";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => oddLengthHexString.AsSpan().FromHexString());
            Assert.That(ex.Message, Does.Contain("Hex string must have an even length"));
        }

        [Test]
        public void FromHexString_EmptyString_ReturnsEmptyByteArray()
        {
            // Arrange
            var emptyHexString = "";

            // Act
            var result = emptyHexString.AsSpan().FromHexString();

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void FromHexString_MixedCaseHexString_ReturnsCorrectByteArray()
        {
            // Arrange
            var hexString = "aBcDeF"; // Mixed-case hex string
            var expected = new byte[] { 0xAB, 0xCD, 0xEF };

            // Act
            var result = hexString.AsSpan().FromHexString();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FromHexString_SpecialCharacters_ThrowsArgumentException()
        {
            // Arrange
            var invalidHexString = "4a6f6@";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => invalidHexString.AsSpan().FromHexString());
            Assert.That(ex.Message, Does.Contain("Invalid hexadecimal character"));
        }

        [Test]
        public void FromHexString_MinimumValidInput_ReturnsSingleByte()
        {
            // Arrange
            var hexString = "0A"; // Single byte
            var expected = new byte[] { 0x0A };

            // Act
            var result = hexString.AsSpan().FromHexString();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }


        [Test]
        public void FromHexString_UpperAndLowerBound_ReturnsByteArray()
        {
            // Arrange
            var hexString = "0aA9fF"; // upper and lower bound hex characters
            var expected = new byte[] { 0x0A, 0xA9, 0xFF };

            // Act
            var result = hexString.AsSpan().FromHexString();

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
