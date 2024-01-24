// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Core.Utilities;
using NUnit.Framework;

namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class TrimmedEncodedBufferTests
    {
        private const int TOTAL_BYTES_LENGTH = 36;

        byte[] buffer =
        {
			/* В */	0xD0, 0x92,
			/* о */	0xD0, 0xBE,
			/* в */	0xD0, 0xB2,
			/* р */	0xD1, 0x80,
			/* е */	0xD0, 0xB5,
			/* м */	0xD0, 0xBC,
			/* я */	0xD1, 0x8F,
			/* п */	0xD0, 0xBF,
			/* у */	0xD1, 0x83,
			/* т */	0xD1, 0x82,
			/* е */	0xD0, 0xB5,
			/* ш */	0xD1, 0x88,
			/* е */	0xD0, 0xB5,
			/* с */	0xD1, 0x81,
			/* т */	0xD1, 0x82,
			/* в */	0xD0, 0xB2,
			/* и */	0xD0, 0xB8,
			/* й */	0xD0, 0xB9
        };

        [TestCaseSource(nameof(TrimmedEncodedBufferTestData))]
        public void TestBufferTrimming(Encoding encoding, int offset, int count, byte[] expectedLeadingBytes, byte[] expectedTrailingBytes, string description)
        {
            var trimmedBuffer = new TrimmedEncodedBuffer(encoding, buffer, offset, count);

            Assert.Multiple(() =>
            {
                Assert.That(trimmedBuffer.LeadingExtraBytesCount, Is.EqualTo(expectedLeadingBytes.Length));
                Assert.That(trimmedBuffer.TrailingExtraBytesCount, Is.EqualTo(expectedTrailingBytes.Length));
                Assert.That(trimmedBuffer.Offset, Is.EqualTo(trimmedBuffer.LeadingExtraBytesOffset + trimmedBuffer.LeadingExtraBytesCount));
            });
            Assert.That(trimmedBuffer.LeadingExtraBytesCount + trimmedBuffer.Length + trimmedBuffer.TrailingExtraBytesCount, Is.EqualTo(count));

            for (var i = 0; i < expectedLeadingBytes.Length; ++i)
            {
                var bufferIndex = i + trimmedBuffer.LeadingExtraBytesOffset;
                Assert.That(trimmedBuffer.Buffer[bufferIndex], Is.EqualTo(expectedLeadingBytes[i]), $"Discrepancy in leading extra bytes at index {bufferIndex}");
            }

            for (var i = 0; i < expectedTrailingBytes.Length; ++i)
            {
                var bufferIndex = i + trimmedBuffer.TrailingExtraBytesOffset;
                Assert.That(trimmedBuffer.Buffer[bufferIndex], Is.EqualTo(expectedTrailingBytes[i]), $"Discrepancy in trailing extra bytes at index {bufferIndex}");
            }
        }

        private static IEnumerable<object[]> TrimmedEncodedBufferTestData()
        {
            yield return new object[] { Encoding.UTF8, 0, TOTAL_BYTES_LENGTH, new byte[] { }, new byte[] { }, "Buffer with no trimming needed" };
            yield return new object[] { Encoding.UTF8, 0, TOTAL_BYTES_LENGTH - 1, new byte[] { }, new byte[] { 0xD0 }, "Buffer with partial multi-byte character at the end" };
            yield return new object[] { Encoding.UTF8, 2, 5, new byte[] { }, new byte[] { 0xD1 }, "Buffer with partial multi-byte character at the end - offset/count mid-buffer" };
            yield return new object[] { Encoding.UTF8, 1, TOTAL_BYTES_LENGTH - 1, new byte[] { 0x92 }, new byte[] { }, "Buffer with partial multi-byte character at the beginning" };
            yield return new object[] { Encoding.UTF8, 1, TOTAL_BYTES_LENGTH - 2, new byte[] { 0x92 }, new byte[] { 0xD0 }, "Buffer with partial multi-byte character at the beginning and end" };
        }

        [TestCaseSource(nameof(LeadingBytesCountTestData))]
        public void Test_GetLeadingBytesCount(Encoding encoding, int offset, int count, int expectedLeadingByteCount, byte[] bytes, string description)
        {
            var trimmedBuffer = new TrimmedEncodedBuffer(encoding, bytes, offset, count);
            Assert.That(trimmedBuffer.LeadingExtraBytesCount, Is.EqualTo(expectedLeadingByteCount));
        }

        private static IEnumerable<object[]> LeadingBytesCountTestData()
        {
            yield return new object[] { Encoding.UTF8, 0, 1, 1, new byte[] { 0x80 }, "Just one byte" };
            yield return new object[] { Encoding.UTF8, 2, 2, 2, new byte[] { 0x01, 0x01, 0x80, 0x80, 0x80 }, "offset > 0, read only 2 bytes" };
            yield return new object[] { Encoding.UTF8, 2, 6, 3, new byte[] { 0x01, 0x01, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80 }, "offset > 0, buffer 6 bytes, but only reads at most 3 leading bytes for UTF-8" };

            yield return new object[] { Encoding.Unicode, 0, 1, 0, new byte[] { 0x80 }, "We only compute leading byte count for UTF-8" };
        }
    }
}
