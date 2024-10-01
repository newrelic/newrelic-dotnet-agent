// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class Decompressor
    {
        public static string DeflateDecompress(byte[] bytes)
        {
            using var compressedStream = new MemoryStream(bytes);
            using var decompressedStream = new MemoryStream();
            using (var deflateStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
            {
                deflateStream.CopyTo(decompressedStream);
            }
            return Encoding.UTF8.GetString(decompressedStream.ToArray());
        }

        public static string GzipDecompress(byte[] bytes)
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
