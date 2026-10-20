// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using NewRelic.Agent.Core.DataTransport.ContinuousProfiling;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.DataTransport.ContinuousProfiling;

[TestFixture]
public class DiagnosticPayloadEncoderTests
{
    private static string Decode(string base64)
    {
        using var input = new MemoryStream(Convert.FromBase64String(base64));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Test]
    public void EncodeGzipBase64_round_trips_json_including_non_ascii()
    {
        const string json = "{\"resourceProfiles\":[{\"name\":\"Ns.Outer`1+<>c.<M>b__0(System.Int32&)\",\"thread\":\"wörker-é-日本\"}]}";

        Assert.That(Decode(DiagnosticPayloadEncoder.EncodeGzipBase64(json)), Is.EqualTo(json));
    }

    [Test]
    public void EncodeGzipBase64_output_is_a_single_line_of_base64_alphabet_only()
    {
        var encoded = DiagnosticPayloadEncoder.EncodeGzipBase64(new string('x', 5000) + "\n" + new string('y', 5000));

        Assert.That(encoded, Does.Match("^[A-Za-z0-9+/]+={0,2}$"));
    }

    [Test]
    public void EncodeGzipBase64_shrinks_repetitive_json_substantially()
    {
        var json = string.Concat(System.Linq.Enumerable.Repeat("{\"frame\":\"NewRelic.Agent.Core.SomeClass.SomeMethod()\"},", 500));

        var encoded = DiagnosticPayloadEncoder.EncodeGzipBase64(json);

        Assert.That(encoded.Length, Is.LessThan(json.Length / 4));
    }

    [Test]
    public void EncodeGzipBase64_handles_empty_string()
    {
        Assert.That(Decode(DiagnosticPayloadEncoder.EncodeGzipBase64(string.Empty)), Is.EqualTo(string.Empty));
    }
}
