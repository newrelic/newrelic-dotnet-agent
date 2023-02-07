// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if !LEGACY_GRPC

using System.IO;
using System.IO.Compression;

namespace NewRelic.Agent.Core.DataTransport
{
    internal class GrpcGzipCompressionProvider : Grpc.Net.Compression.ICompressionProvider
    {
        private readonly CompressionLevel _compressionLevel;

        public GrpcGzipCompressionProvider(CompressionLevel compressionLevel)
        {
            _compressionLevel = compressionLevel;
        }

        public string EncodingName => "gzip";

        public Stream CreateCompressionStream(Stream stream, CompressionLevel? _)
        {
            return new GZipStream(stream, _compressionLevel, leaveOpen: true);
        }

        public Stream CreateDecompressionStream(Stream stream)
        {
            return new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        }
    }
}

#endif
