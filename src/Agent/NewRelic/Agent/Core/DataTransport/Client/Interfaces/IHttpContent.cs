// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;

namespace NewRelic.Agent.Core.DataTransport.Client.Interfaces
{
    public interface IHttpContent
    {
        string ContentType { get; set; }
        string SerializedData { get; set; }

        /// <summary>
        ///     Content headers
        /// </summary>
        List<KeyValuePair<string, string>> Headers { get; }


        byte[] PayloadBytes { get; }
        long UncompressedByteCount { get; }
        bool IsCompressed { get; }
        string CompressionType { get; }
        string Encoding { get; }
    }
}
