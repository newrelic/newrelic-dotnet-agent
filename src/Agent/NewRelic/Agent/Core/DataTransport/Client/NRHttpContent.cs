// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Text;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.DataTransport.Client.Interfaces;

namespace NewRelic.Agent.Core.DataTransport.Client
{
    /// <summary>
    /// Abstraction of content sent in client requests
    /// </summary>
    public class NRHttpContent : IHttpContent
    {
        private readonly IConfiguration _configuration;
        private CollectorRequestPayload _collectorRequestPayload;

        private bool _payloadInitialized;
        private long _uncompressedByteCount;

        public NRHttpContent(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ContentType { get; set; }

        public string Encoding
        {
            get
            {
                if (!_payloadInitialized)
                {
                    InitializePayload();
                }

                return _collectorRequestPayload.IsCompressed
                    ? _collectorRequestPayload.CompressionType.ToLower()
                    : "identity";
            }
        }

        public string SerializedData { get; set; }

        public byte[] PayloadBytes
        {
            get
            {
                if (!_payloadInitialized)
                {
                    InitializePayload();
                }

                return _collectorRequestPayload.Data;
            }
        }

        public long UncompressedByteCount
        {
            get
            {
                if (!_payloadInitialized)
                {
                    InitializePayload();
                }

                return _uncompressedByteCount;
            }
        }

        public List<KeyValuePair<string, string>> Headers { get; } = new List<KeyValuePair<string, string>>();

        public bool IsCompressed
        {
            get
            {
                if (!_payloadInitialized)
                {
                    InitializePayload();
                }

                return _collectorRequestPayload.IsCompressed;
            }
        }

        public string CompressionType
        {
            get
            {
                if (!_payloadInitialized)
                {
                    InitializePayload();
                }

                return _collectorRequestPayload.CompressionType;
            }
        }

        private void InitializePayload()
        {
            if (_payloadInitialized)
            {
                return;
            }

            var bytes = new UTF8Encoding().GetBytes(SerializedData);
            _uncompressedByteCount = bytes.Length;

            _collectorRequestPayload = GetRequestPayload(bytes);

            _payloadInitialized = true;

            if (_collectorRequestPayload.Data.Length > _configuration.CollectorMaxPayloadSizeInBytes)
            {
                throw new PayloadSizeExceededException();
            }
        }

        private CollectorRequestPayload GetRequestPayload(byte[] bytes)
        {
            var shouldCompress = bytes.Length >= Constants.CompressMinimumByteLength;

            string compressionType = null;
            if (shouldCompress)
            {
                compressionType = _configuration.CompressedContentEncoding;
                bytes = DataCompressor.Compress(bytes, compressionType);
            }

            var payload = new CollectorRequestPayload(shouldCompress, compressionType, bytes);

            return payload;
        }
    }
}
