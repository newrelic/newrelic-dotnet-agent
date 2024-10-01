// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport
{
    [TestFixture]
    public class DataCompressorTests
    {
        [Test]
        [TestCase("")]
        [TestCase("foo")]
        [TestCase("Զԣש", Description = "Some UTF-8 characters")]
        public void DeflateCompressedDataShouldDecompressToSameValue(string input)
        {
            var compressed = DataCompressor.Compress(input);
            var decompressed = DataCompressor.Decompress(compressed);

            Assert.That(decompressed, Is.EqualTo(input));
        }

        [Test]
        [TestCase("")]
        [TestCase("foo")]
        [TestCase("Զԣש", Description = "Some UTF-8 characters")]
        public void GZipCompressedDataShouldDecompressToSameValue(string input)
        {
            var compressed = DataCompressor.Compress(new UTF8Encoding().GetBytes(input), DataCompressor.GzipCompression);
            var decompressed = DecompressGzip(compressed);
            Assert.That(decompressed, Is.EqualTo(input));
        }

        [Test]
        public void DefaultCompressionShouldBeDeflate()
        {
            const string input = "input";
            var defaultCompression = DataCompressor.Compress(input);
            var explicitCompression = DataCompressor.Compress(new UTF8Encoding().GetBytes(input), DataCompressor.DeflateCompression);
            Assert.That(explicitCompression, Is.EqualTo(defaultCompression));
        }

        [Test]
        public void ShouldThrowWhenInvalidCompressionType()
        {
            const string input = "input";
            const string invalidCompression = "invalidType";

            Assert.Throws<ArgumentException>(() => DataCompressor.Compress(new UTF8Encoding().GetBytes(input), invalidCompression));
        }

        [Test]
        [TestCase("DEFLATE")]
        [TestCase("GZIP")]
        public void ShouldHandleInconsistentCompressionCasing(string compressionType)
        {
            const string input = "input";
            Assert.DoesNotThrow(() => DataCompressor.Compress(new UTF8Encoding().GetBytes(input), compressionType));
        }

        private static string DecompressGzip(byte[] compressedBytes)
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream);
            }
            return Encoding.UTF8.GetString(decompressedStream.ToArray());
        }
    }
}
