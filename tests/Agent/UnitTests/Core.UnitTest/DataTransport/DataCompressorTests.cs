using System;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using JetBrains.Annotations;
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
        public void DeflateCompressedDataShouldDecompressToSameValue([NotNull] String input)
        {
            var compressed = DataCompressor.Compress(input);
            var decompressed = DataCompressor.Decompress(compressed);

            Assert.AreEqual(input, decompressed);
        }

        [Test]
        [TestCase("")]
        [TestCase("foo")]
        [TestCase("Զԣש", Description = "Some UTF-8 characters")]
        public void GZipCompressedDataShouldDecompressToSameValue([NotNull] String input)
        {
            var compressed = DataCompressor.Compress(new UTF8Encoding().GetBytes(input), DataCompressor.GzipCompression);
            var decompressed = DecompressGzip(compressed);
            Assert.AreEqual(input, decompressed);
        }

        [Test]
        public void DefaultCompressionShouldBeDeflate()
        {
            const String input = "input";
            var defaultCompression = DataCompressor.Compress(input);
            var explicitCompression = DataCompressor.Compress(new UTF8Encoding().GetBytes(input), DataCompressor.DeflateCompression);
            Assert.AreEqual(defaultCompression, explicitCompression);
        }

        [Test]
        public void ShouldThrowWhenInvalidCompressionType()
        {
            const String input = "input";
            const String invalidCompression = "invalidType";

            Assert.Throws<ArgumentException>(() => DataCompressor.Compress(new UTF8Encoding().GetBytes(input), invalidCompression));
        }

        [Test]
        [TestCase("DEFLATE")]
        [TestCase("GZIP")]
        public void ShouldHandleInconsistentCompressionCasing(String compressionType)
        {
            const String input = "input";
            Assert.DoesNotThrow(() => DataCompressor.Compress(new UTF8Encoding().GetBytes(input), compressionType));
        }

        private static String DecompressGzip(Byte[] compressedBytes)
        {
            using (var memoryStream = new MemoryStream())
            using (var inflaterStream = new GZipInputStream(memoryStream))
            using (var streamReader = new StreamReader(inflaterStream, Encoding.UTF8))
            {
                memoryStream.Write(compressedBytes, 0, compressedBytes.Length);
                memoryStream.Flush();
                memoryStream.Position = 0;
                return streamReader.ReadToEnd();
            }
        }
    }
}
