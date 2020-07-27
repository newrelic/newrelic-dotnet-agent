using System;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using JetBrains.Annotations;

namespace NewRelic.Agent.Core.DataTransport
{
    public static class DataCompressor
    {
        public const String DeflateCompression = "deflate";
        public const String GzipCompression = "gzip";

        [NotNull]
        public static Byte[] Compress([NotNull] String data)
        {
            var bytes = new UTF8Encoding().GetBytes(data);
            return Compress(bytes);
        }

        [NotNull]
        public static Byte[] Compress([NotNull] Byte[] bytes)
        {
            return Compress(bytes, DeflateCompression);
        }

        [NotNull]
        public static Byte[] Compress([NotNull] Byte[] bytes, [NotNull] String compressionType)
        {
            using (var stream = new MemoryStream(bytes.Length))
            using (var outputStream = GetCompressionOutputStream(stream, compressionType))
            {
                outputStream.Write(bytes, 0, bytes.Length);
                outputStream.Flush();
                outputStream.Finish();
                return stream.ToArray();
            }
        }

        private static DeflaterOutputStream GetCompressionOutputStream(Stream stream, String requestedCompression)
        {
            var compressionType = requestedCompression.ToLower();
            switch (compressionType)
            {
                case DeflateCompression:
                    return new DeflaterOutputStream(stream, new Deflater(Deflater.DEFAULT_COMPRESSION));
                case GzipCompression:
                    return new GZipOutputStream(stream);
                default:
                    throw new ArgumentException($"compressionType is not one of the valid options: {compressionType}");
            }
        }

        [NotNull]
        public static String Decompress([NotNull] Byte[] compressedBytes)
        {
            using (var memoryStream = new MemoryStream())
            using (var inflaterStream = new InflaterInputStream(memoryStream, new Inflater()))
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
