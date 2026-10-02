// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.Agent.Core.DataTransport.ContinuousProfiling;

/// <summary>
/// Shrinks the CP diagnostic JSON into a single-line, log-safe string. Gzip output is arbitrary bytes, so it
/// is base64-encoded (ASCII only, no newlines) before it goes near a text log sink. CompressionLevel.Fastest
/// matches OtlpProfilesHttpDispatcher.Gzip -- the input is text-heavy and repetitive, so the cheap level
/// already gets most of the win.
/// </summary>
public static class DiagnosticPayloadEncoder
{
    public static string EncodeGzipBase64(string text)
    {
        var raw = Encoding.UTF8.GetBytes(text);

        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(raw, 0, raw.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
