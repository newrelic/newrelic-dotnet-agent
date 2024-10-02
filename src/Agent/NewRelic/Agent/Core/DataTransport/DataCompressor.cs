// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.Agent.Core.DataTransport
{
    public static class DataCompressor
    {
        public const string DeflateCompression = "deflate";
        public const string GzipCompression = "gzip";

        public static byte[] Compress(string data)
        {
            var bytes = new UTF8Encoding().GetBytes(data);
            return Compress(bytes);
        }

        public static byte[] Compress(byte[] bytes)
        {
            return Compress(bytes, DeflateCompression);
        }

        public static byte[] Compress(byte[] bytes, string compressionType)
        {
            using var compressedStream = new MemoryStream();

            using (var compressor = GetCompressionOutputStream(compressedStream, compressionType))
            {
                compressor.Write(bytes, 0, bytes.Length);
            }

            return compressedStream.ToArray();
        }

        private static Stream GetCompressionOutputStream(Stream stream, string requestedCompression)
        {
            var compressionType = requestedCompression.ToLower();
            switch (compressionType)
            {
                case DeflateCompression:
                    return new DeflateStream(stream, CompressionMode.Compress, true);
                case GzipCompression:
                    return new GZipStream(stream, CompressionMode.Compress, true);
                default:
                    throw new ArgumentException($"compressionType is not one of the valid options: {compressionType}");
            }
        }

        public static string Decompress(byte[] compressedBytes, string compressionType)
        {
            return compressionType.ToLower() switch
            {
                DeflateCompression => DecompressDeflate(compressedBytes),
                GzipCompression => DecompressGZip(compressedBytes),
                _ => throw new ArgumentException($"compressionType is not one of the valid options: {compressionType}")
            };
        }

        public static string DecompressDeflate(byte[] compressedBytes)
        {
            using var compressedStream = new MemoryStream(compressedBytes);
            using var decompressedStream = new MemoryStream();
            using (var decompressor = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                decompressor.CopyTo(decompressedStream);
            }
            return Encoding.UTF8.GetString(decompressedStream.ToArray());
        }

        public static string DecompressGZip(byte[] bytes)
        {
            using var compressedStream = new MemoryStream(bytes);
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress))
            {
                gzipStream.CopyTo(decompressedStream);
            }
            return Encoding.UTF8.GetString(decompressedStream.ToArray());
        }
    }
}
