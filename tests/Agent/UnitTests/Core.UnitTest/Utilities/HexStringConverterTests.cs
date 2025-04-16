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
            var result = HexStringConverter.FromHexString(hexString.AsSpan());

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void FromHexString_InvalidHexCharacter_ThrowsArgumentException()
        {
            // Arrange
            var invalidHexString = "4a6f6g";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => HexStringConverter.FromHexString(invalidHexString.AsSpan()));
            Assert.That(ex.Message, Does.Contain("Invalid hexadecimal character"));
        }

        [Test]
        public void FromHexString_OddLengthHexString_ThrowsArgumentException()
        {
            // Arrange
            var oddLengthHexString = "4a6f6";

            // Act & Assert
            var ex = Assert.Throws<ArgumentException>(() => HexStringConverter.FromHexString(oddLengthHexString.AsSpan()));
            Assert.That(ex.Message, Does.Contain("Hex string must have an even length"));
        }

        [Test]
        public void FromHexString_EmptyString_ReturnsEmptyByteArray()
        {
            // Arrange
            var emptyHexString = "";

            // Act
            var result = HexStringConverter.FromHexString(emptyHexString.AsSpan());

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void FromHexString_LargeHexString_ReturnsCorrectByteArray()
        {
            // Arrange
            var hexString = new string('A', 1000); // 500 bytes of 0xAA
            var expected = new byte[500];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = 0xAA;
            }

            // Act
            var result = HexStringConverter.FromHexString(hexString.AsSpan());

            // Assert
            Assert.That(result, Is.EqualTo(expected));
        }
    }
}
