// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;

namespace NewRelic.Agent.Core.DataTransport;

/// <summary>
/// Pure gRPC length-prefixed message framing for the hand-rolled unary transport (no grpc-dotnet).
/// A gRPC data frame is: [1-byte compression flag][4-byte big-endian length][message bytes].
/// </summary>
public static class GrpcUnaryFraming
{
    public const int FrameHeaderSize = 5;

    /// <summary>
    /// Wraps a serialized protobuf payload in an uncompressed gRPC data frame.
    /// </summary>
    public static byte[] Frame(byte[] payload)
    {
        var framed = new byte[FrameHeaderSize + payload.Length];
        framed[0] = 0; // not compressed
        framed[1] = (byte)((payload.Length >> 24) & 0xFF);
        framed[2] = (byte)((payload.Length >> 16) & 0xFF);
        framed[3] = (byte)((payload.Length >> 8) & 0xFF);
        framed[4] = (byte)(payload.Length & 0xFF);
        Buffer.BlockCopy(payload, 0, framed, FrameHeaderSize, payload.Length);
        return framed;
    }

    /// <summary>
    /// Extracts the message bytes from a gRPC response frame, decompressing if the frame is gzip-flagged.
    /// Returns null when there is no complete message (empty/header-only body, or a truncated frame) - which
    /// the unary transport treats as "no reply" / failure.
    /// </summary>
    public static byte[] TryGetMessage(byte[] response)
    {
        if (response == null || response.Length <= FrameHeaderSize)
        {
            return null;
        }

        var compressed = response[0] == 1;
        var length = (response[1] << 24) | (response[2] << 16) | (response[3] << 8) | response[4];

        if (length <= 0 || FrameHeaderSize + length > response.Length)
        {
            return null;
        }

        var message = new byte[length];
        Buffer.BlockCopy(response, FrameHeaderSize, message, 0, length);

        if (compressed)
        {
            using (var input = new MemoryStream(message))
            using (var gzip = new GZipStream(input, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gzip.CopyTo(output);
                message = output.ToArray();
            }
        }

        return message;
    }
}
