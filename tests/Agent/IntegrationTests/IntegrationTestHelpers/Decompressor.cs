using System;
using System.IO;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace NewRelic.Agent.IntegrationTestHelpers
{
    public class Decompressor
    {
        public static string DeflateDecompress(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream())
            using (var inflaterStream = new InflaterInputStream(memoryStream, new Inflater()))
            using (var streamReader = new StreamReader(inflaterStream))
            {
                memoryStream.Write(bytes, 0, bytes.Length);
                memoryStream.Flush();
                memoryStream.Position = 0;
                return streamReader.ReadToEnd();
            }
        }

        public static string GzipDecompress(byte[] bytes)
        {
            using (var memoryStream = new MemoryStream())
            using (var inflaterStream = new GZipInputStream(memoryStream))
            using (var streamReader = new StreamReader(inflaterStream))
            {
                memoryStream.Write(bytes, 0, bytes.Length);
                memoryStream.Flush();
                memoryStream.Position = 0;
                return streamReader.ReadToEnd();
            }
        }
    }
}
