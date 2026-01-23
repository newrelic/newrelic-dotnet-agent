// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.Text;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace NewRelic.Agent.Core.DataTransport;

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
        using (var stream = new MemoryStream(bytes.Length))
        using (var outputStream = GetCompressionOutputStream(stream, compressionType))
        {
            outputStream.Write(bytes, 0, bytes.Length);
            outputStream.Flush();
            outputStream.Finish();
            return stream.ToArray();
        }
    }

    private static DeflaterOutputStream GetCompressionOutputStream(Stream stream, string requestedCompression)
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

    public static string Decompress(byte[] compressedBytes)
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