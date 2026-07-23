// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using Google.Protobuf;
using NewRelic.Agent.Core.ContinuousProfiling;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Profiles.V1Development;
using OpenTelemetry.Proto.Resource.V1;

namespace NewRelic.Agent.Core.UnitTest.ContinuousProfiling;

[TestFixture]
public class ProfilesTransportTests
{
    [Test]
    public void Send_invokes_http_dispatch_with_the_serialized_request_bytes_and_endpoint()
    {
        byte[] dispatchedBytes = null;
        string dispatchedEndpoint = null;
        var transport = new ProfilesTransport((bytes, endpoint) =>
        {
            dispatchedBytes = bytes;
            dispatchedEndpoint = endpoint;
            return new ProfilesSendResult(true, 200, string.Empty);
        }, "https://otlp.nr-data.net/v1/profiles");

        var request = BuildNonEmptyRequest();
        transport.Send(request);

        Assert.Multiple(() =>
        {
            Assert.That(dispatchedEndpoint, Is.EqualTo("https://otlp.nr-data.net/v1/profiles"));
            Assert.That(dispatchedBytes, Is.EqualTo(request.ToByteArray()), "The dispatched bytes must be the serialized request.");
        });
    }

    [Test]
    public void Send_invokes_http_dispatch_even_for_an_empty_request()
    {
        var dispatched = false;
        var transport = new ProfilesTransport((bytes, endpoint) => { dispatched = true; return new ProfilesSendResult(true, 200, string.Empty); }, "http://unused");

        transport.Send(new ExportProfilesServiceRequest());

        Assert.That(dispatched, Is.True, "Send must post whatever it built; gating happens upstream (CP-enabled), not here.");
    }

    [Test]
    public void Send_does_not_throw_when_the_dispatch_reports_failure()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(false, 500, "error"), "http://unused");
        Assert.That(() => transport.Send(BuildNonEmptyRequest()), Throws.Nothing);
    }

    [Test]
    public void Send_constructs_without_throwing_given_endpoint_and_dispatch_delegate()
    {
        Assert.That(() => new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "https://otlp.nr-data.net/v1/profiles"),
            Throws.Nothing);
    }

    [Test]
    public void Send_handles_null_resource_profiles_without_throwing()
    {
        var transport = new ProfilesTransport((bytes, endpoint) => new ProfilesSendResult(true, 200, string.Empty), "http://unused");
        Assert.That(() => transport.Send(new ExportProfilesServiceRequest()), Throws.Nothing);
    }

    // The Finest diagnostic log line carries ToDiagnosticJson(request); testing the serialization directly
    // avoids capturing the static logger while still pinning the payload's shape.

    [Test]
    public void ToDiagnosticJson_is_compact_single_line_valid_json()
    {
        var json = ProfilesTransport.ToDiagnosticJson(BuildNonEmptyRequest());

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Not.Contain("\n"), "Diagnostic JSON must be compact (single line), like other collector payloads.");
            Assert.That(json, Does.Contain("worker-1"), "The sample's thread.name attribute value should be present.");
            // Valid JSON with the proto3 top-level shape (camelCase field names).
            var root = JObject.Parse(json);
            Assert.That(root["resourceProfiles"], Is.Not.Null, "Serialized request should carry resourceProfiles.");
        });
    }

    [Test]
    public void ToDiagnosticJson_emits_frame_name_special_chars_literally_not_unicode_escaped()
    {
        // JsonFormatter emits printable ASCII literally, so the chars common in .NET frame names -- nested
        // '+', closure '<'/'>', generic-arity backtick, byref '&' -- must appear as-is, not \uXXXX escaped.
        const string frameName = "Ns.Outer`1+<>c.<M>b__0(System.Int32&)";

        var dictionary = new ProfilesDictionary();
        dictionary.StringTable.Add(frameName);
        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };

        var json = ProfilesTransport.ToDiagnosticJson(request);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain(frameName), "Frame-name special chars should be emitted literally.");
            Assert.That(json, Does.Not.Contain("\\u00"), "No \\uXXXX HTML-escaping of printable ASCII.");
        });
    }

    [Test]
    public void ToDiagnosticJson_renders_link_ids_as_lowercase_hex_not_base64()
    {
        // trace_id/span_id are proto `bytes` -> proto3 JSON base64. The diagnostic log rewrites them to
        // lowercase hex so they're greppable against the W3C-hex ids used elsewhere in the logs.
        var traceId = new byte[] { 0x1c, 0xb9, 0xb2, 0x2a, 0x7b, 0xfd, 0x43, 0x3d, 0x29, 0xdc, 0xdf, 0xe1, 0x1a, 0xb7, 0xfe, 0x27 };
        var spanId = new byte[] { 0x37, 0x83, 0xcc, 0xde, 0xba, 0x84, 0x13, 0x91 };

        var dictionary = new ProfilesDictionary();
        dictionary.LinkTable.Add(new Link
        {
            TraceId = ByteString.CopyFrom(traceId),
            SpanId = ByteString.CopyFrom(spanId),
        });
        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };

        var json = ProfilesTransport.ToDiagnosticJson(request);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"traceId\":\"1cb9b22a7bfd433d29dcdfe11ab7fe27\""), "traceId should be lowercase hex.");
            Assert.That(json, Does.Contain("\"spanId\":\"3783ccdeba841391\""), "spanId should be lowercase hex.");
            Assert.That(json, Does.Not.Contain("=="), "No base64-padded ids should remain.");
        });
    }

    private static ExportProfilesServiceRequest BuildNonEmptyRequest()
    {
        var dictionary = new ProfilesDictionary();
        dictionary.StringTable.Add(string.Empty);
        dictionary.StringTable.Add("thread.name");
        dictionary.StringTable.Add("worker-1");
        dictionary.StringTable.Add("A()");

        dictionary.FunctionTable.Add(new Function());
        dictionary.FunctionTable.Add(new Function { NameStrindex = 3 });

        dictionary.LocationTable.Add(new Location());
        var location = new Location();
        location.Lines.Add(new Line { FunctionIndex = 1 });
        dictionary.LocationTable.Add(location);

        dictionary.StackTable.Add(new Stack());
        var stack = new Stack();
        stack.LocationIndices.Add(1);
        dictionary.StackTable.Add(stack);

        dictionary.AttributeTable.Add(new KeyValueAndUnit());
        dictionary.AttributeTable.Add(new KeyValueAndUnit
        {
            KeyStrindex = 1,
            Value = new AnyValue { StringValue = "worker-1" }
        });

        var sample = new Sample { StackIndex = 1 };
        sample.Values.Add(1L);
        sample.AttributeIndices.Add(1);

        var profile = new Profile();
        profile.Samples.Add(sample);

        var scopeProfiles = new ScopeProfiles();
        scopeProfiles.Profiles.Add(profile);

        var resourceProfiles = new ResourceProfiles { Resource = new Resource() };
        resourceProfiles.ScopeProfiles.Add(scopeProfiles);

        var request = new ExportProfilesServiceRequest { Dictionary = dictionary };
        request.ResourceProfiles.Add(resourceProfiles);
        return request;
    }
}
