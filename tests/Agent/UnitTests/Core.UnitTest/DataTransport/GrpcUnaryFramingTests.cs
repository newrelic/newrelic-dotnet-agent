// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using System.IO.Compression;
using System.Text;
using NUnit.Framework;

namespace NewRelic.Agent.Core.DataTransport;

[TestFixture]
public class GrpcUnaryFramingTests
{
    [Test]
    public void Frame_PrependsFiveByteHeader_WithUncompressedFlagAndBigEndianLength()
    {
        var payload = new byte[] { 10, 20, 30 };

        var framed = GrpcUnaryFraming.Frame(payload);

        Assert.That(framed.Length, Is.EqualTo(5 + payload.Length));
        Assert.That(framed[0], Is.EqualTo(0), "compression flag should be 0 (uncompressed)");
        Assert.That(framed[1], Is.EqualTo(0));
        Assert.That(framed[2], Is.EqualTo(0));
        Assert.That(framed[3], Is.EqualTo(0));
        Assert.That(framed[4], Is.EqualTo(3), "big-endian length low byte");
        Assert.That(new[] { framed[5], framed[6], framed[7] }, Is.EqualTo(payload));
    }

    [Test]
    public void TryGetMessage_ReturnsPayload_ForUncompressedFrame()
    {
        var payload = Encoding.UTF8.GetBytes("hello-record-status");
        var framed = GrpcUnaryFraming.Frame(payload);

        var result = GrpcUnaryFraming.TryGetMessage(framed);

        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void TryGetMessage_DecompressesPayload_WhenCompressionFlagSet()
    {
        var payload = Encoding.UTF8.GetBytes("compressed-record-status-body");
        byte[] compressed;
        using (var mso = new MemoryStream())
        {
            using (var gz = new GZipStream(mso, CompressionMode.Compress, leaveOpen: true))
            {
                gz.Write(payload, 0, payload.Length);
            }
            compressed = mso.ToArray();
        }

        var frame = new byte[5 + compressed.Length];
        frame[0] = 1; // compressed
        frame[1] = (byte)((compressed.Length >> 24) & 0xFF);
        frame[2] = (byte)((compressed.Length >> 16) & 0xFF);
        frame[3] = (byte)((compressed.Length >> 8) & 0xFF);
        frame[4] = (byte)(compressed.Length & 0xFF);
        System.Buffer.BlockCopy(compressed, 0, frame, 5, compressed.Length);

        var result = GrpcUnaryFraming.TryGetMessage(frame);

        Assert.That(result, Is.EqualTo(payload));
    }

    [Test]
    public void TryGetMessage_ReturnsNull_ForEmptyBody()
    {
        Assert.That(GrpcUnaryFraming.TryGetMessage(new byte[0]), Is.Null);
    }

    [Test]
    public void TryGetMessage_ReturnsNull_ForHeaderOnlyBody()
    {
        // 5-byte header declaring length 0, no message.
        Assert.That(GrpcUnaryFraming.TryGetMessage(new byte[] { 0, 0, 0, 0, 0 }), Is.Null);
    }

    [Test]
    public void TryGetMessage_ReturnsNull_WhenDeclaredLengthExceedsBuffer()
    {
        // Header says 100 bytes but only 2 follow.
        Assert.That(GrpcUnaryFraming.TryGetMessage(new byte[] { 0, 0, 0, 0, 100, 1, 2 }), Is.Null);
    }
}
