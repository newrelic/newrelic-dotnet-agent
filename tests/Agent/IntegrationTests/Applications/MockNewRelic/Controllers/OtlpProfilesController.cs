// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewRelic.IntegrationTests.Models;
using OpenTelemetry.Proto.Collector.Profiles.V1Development;

namespace MockNewRelic.Controllers;

/// <summary>
/// Independent receiver for the continuous-profiling OTLP/HTTP export. Decodes the gzip-compressed
/// protobuf body the agent's <c>OtlpProfilesHttpDispatcher</c> posts to <c>/v1/profiles</c> as a real
/// <c>ExportProfilesServiceRequest</c> and records a structural summary the integration tests can read
/// back -- so the CP test tier validates the actual bytes on the wire, not just the agent's debug log.
/// </summary>
[ApiController]
[Route("v1/profiles")]
public class OtlpProfilesController : ControllerBase
{
    private const int MaxStoredSummaries = 100;
    private static readonly ConcurrentQueue<ProfilesSummaryDto> _summaries = new ConcurrentQueue<ProfilesSummaryDto>();

    // Test control: when non-zero, every profiles POST returns this HTTP status instead of 200, so an
    // integration test can drive the agent's send-failure / backoff-retry path against a genuinely failing
    // ingest endpoint. This is exactly the risk surface the removed send-failure no-send guard created and
    // that no test otherwise covers -- the controller previously always returned Ok. The request body is
    // still fully read so the connection is consumed cleanly; the payload is NOT recorded (a failing collector
    // does not ack). Reset to 0 to resume normal 200 responses.
    private static volatile int _forcedResponseStatusCode;

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var contentType = Request.ContentType?.ToLowerInvariant();
        if (contentType == null || !contentType.Contains("application/x-protobuf"))
        {
            return StatusCode(415, "{}");
        }

        if (!Request.Headers.ContainsKey("api-key"))
        {
            return StatusCode(401, "{}");
        }

        byte[] bodyBytes = await ReadRequestBodyAsync(Request);

        // Read the body first (above) so the connection is drained, THEN honor a forced failure status.
        var forcedStatus = _forcedResponseStatusCode;
        if (forcedStatus != 0)
        {
            return StatusCode(forcedStatus, "{}");
        }

        ExportProfilesServiceRequest request;
        try
        {
            request = ExportProfilesServiceRequest.Parser.ParseFrom(bodyBytes);
        }
        catch
        {
            return StatusCode(400, "{}");
        }

        var summary = Summarize(request);
        _summaries.Enqueue(summary);

        while (_summaries.Count > MaxStoredSummaries && _summaries.TryDequeue(out _)) { }

        return Ok("{}");
    }

    /// <summary>
    /// Get the most recent received profile summaries (newest first).
    /// </summary>
    [HttpGet("collected")]
    public IActionResult GetCollected([FromQuery] int n = 10)
    {
        if (n <= 0) n = 1;
        var items = _summaries.Reverse().Take(n).ToList();
        return Ok(items);
    }

    /// <summary>
    /// Get the count of received profile summaries.
    /// </summary>
    [HttpGet("count")]
    public IActionResult GetCount()
    {
        return Ok(_summaries.Count);
    }

    /// <summary>
    /// Clear all received profile summaries.
    /// </summary>
    [HttpPost("clear")]
    public IActionResult Clear()
    {
        while (_summaries.TryDequeue(out _)) { }
        return Ok("{}");
    }

    /// <summary>
    /// Test control: force every subsequent profiles POST to return the given HTTP status (e.g. 500), so a
    /// test can exercise the agent's send-failure / backoff-retry path. Pass 0 to resume normal 200s.
    /// </summary>
    [HttpGet("response-status")]
    public IActionResult SetResponseStatus([FromQuery] int status)
    {
        _forcedResponseStatusCode = status;
        return Ok("{}");
    }

    private static async Task<byte[]> ReadRequestBodyAsync(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        using var ms = new MemoryStream();
        if (request.Body == null)
        {
            return Array.Empty<byte>();
        }

        var encoding = request.Headers["Content-Encoding"].ToString().ToLowerInvariant();
        if (encoding.Contains("gzip"))
        {
            await using var gzip = new GZipStream(request.Body, CompressionMode.Decompress, leaveOpen: true);
            await gzip.CopyToAsync(ms);
        }
        else if (encoding.Contains("deflate"))
        {
            await using var deflate = new DeflateStream(request.Body, CompressionMode.Decompress, leaveOpen: true);
            await deflate.CopyToAsync(ms);
        }
        else
        {
            await request.Body.CopyToAsync(ms);
        }

        return ms.ToArray();
    }

    // profile.frame.type marker for the synthetic native thread-entry frame, mirrored from
    // OtlpProfileBuilder.NativeFrameName -- a real resolved managed name is neither this nor a placeholder.
    private const string NativeFrameName = "Native.Function Call";

    // Prefix the native profiler emits for a frame whose CLR metadata could not be resolved
    // (ContinuousProfiler.h AssembleFrameName). A string table made up only of these is NOT real content.
    private const string UnknownFramePlaceholderPrefix = "UnknownClass.UnknownMethod(";

    private const string ServiceNameKey = "service.name";
    private const string EntityGuidKey = "entity.guid";
    private const string HostKey = "host";

    private static ProfilesSummaryDto Summarize(ExportProfilesServiceRequest request)
    {
        var resourceProfileCount = request.ResourceProfiles.Count;
        var scopeProfileCount = 0;
        var profileCount = 0;
        var sampleCount = 0;

        var dictionary = request.Dictionary;
        var stringTable = dictionary?.StringTable;

        // Resolve one frame's name: location -> line.function_index -> function.name_strindex -> string_table.
        // Any out-of-range index (a malformed payload) yields null rather than throwing.
        string FrameName(int locationIndex)
        {
            if (dictionary?.LocationTable == null || locationIndex < 0 || locationIndex >= dictionary.LocationTable.Count)
                return null;
            var location = dictionary.LocationTable[locationIndex];
            if (location.Lines.Count == 0)
                return null;
            var functionIndex = location.Lines[0].FunctionIndex;
            if (dictionary.FunctionTable == null || functionIndex < 0 || functionIndex >= dictionary.FunctionTable.Count)
                return null;
            var nameStrindex = dictionary.FunctionTable[functionIndex].NameStrindex;
            if (stringTable == null || nameStrindex < 0 || nameStrindex >= stringTable.Count)
                return null;
            return stringTable[nameStrindex];
        }

        static bool IsRealFrameName(string name) =>
            !string.IsNullOrEmpty(name)
            && name != NativeFrameName
            && !name.StartsWith(UnknownFramePlaceholderPrefix, StringComparison.Ordinal)
            && name.Contains('.'); // real managed frames are "Type.Method(...)"

        var maxFramesInAnySample = 0;
        var samplesWithFramesCount = 0;
        var hasRealFrameName = false;
        string exampleRealFrameName = null;

        foreach (var rp in request.ResourceProfiles)
        {
            scopeProfileCount += rp.ScopeProfiles.Count;
            foreach (var sp in rp.ScopeProfiles)
            {
                profileCount += sp.Profiles.Count;
                foreach (var profile in sp.Profiles)
                {
                    sampleCount += profile.Samples.Count;
                    foreach (var sample in profile.Samples)
                    {
                        var stackIndex = sample.StackIndex;
                        if (dictionary?.StackTable == null || stackIndex < 0 || stackIndex >= dictionary.StackTable.Count)
                            continue;

                        var locationIndices = dictionary.StackTable[stackIndex].LocationIndices;
                        var frameCount = locationIndices.Count;
                        if (frameCount > maxFramesInAnySample)
                            maxFramesInAnySample = frameCount;
                        if (frameCount > 0)
                            samplesWithFramesCount++;

                        foreach (var locationIndex in locationIndices)
                        {
                            var name = FrameName(locationIndex);
                            if (IsRealFrameName(name))
                            {
                                hasRealFrameName = true;
                                exampleRealFrameName ??= name;
                            }
                        }
                    }
                }
            }
        }

        // Resource identity attributes from the first resource (the agent emits a single ResourceProfiles).
        var resourceAttributeKeys = new List<string>();
        var hasServiceName = false;
        string serviceName = null;
        var hasEntityGuid = false;
        string entityGuid = null;
        var hasHost = false;

        var firstResource = request.ResourceProfiles.Count > 0 ? request.ResourceProfiles[0].Resource : null;
        if (firstResource != null)
        {
            foreach (var attr in firstResource.Attributes)
            {
                resourceAttributeKeys.Add(attr.Key);
                switch (attr.Key)
                {
                    case ServiceNameKey:
                        hasServiceName = !string.IsNullOrEmpty(attr.Value?.StringValue);
                        serviceName = attr.Value?.StringValue;
                        break;
                    case EntityGuidKey:
                        hasEntityGuid = !string.IsNullOrEmpty(attr.Value?.StringValue);
                        entityGuid = attr.Value?.StringValue;
                        break;
                    case HostKey:
                        hasHost = attr.Value != null;
                        break;
                }
            }
        }

        // string_table[0] MUST be "" and present, so a table with more than one entry is the signal
        // that the profile carries real (non-reserved) string data.
        var stringTableSize = stringTable?.Count ?? 0;

        var structurallyValid = resourceProfileCount > 0 && scopeProfileCount > 0 && profileCount > 0 && stringTableSize > 1;

        return new ProfilesSummaryDto
        {
            ReceivedAtUtc = DateTime.UtcNow,
            ResourceProfileCount = resourceProfileCount,
            ScopeProfileCount = scopeProfileCount,
            ProfileCount = profileCount,
            SampleCount = sampleCount,
            StringTableSize = stringTableSize,
            MaxFramesInAnySample = maxFramesInAnySample,
            SamplesWithFramesCount = samplesWithFramesCount,
            HasRealFrameName = hasRealFrameName,
            ExampleRealFrameName = exampleRealFrameName,
            ResourceAttributeKeys = resourceAttributeKeys,
            HasServiceName = hasServiceName,
            ServiceName = serviceName,
            HasEntityGuid = hasEntityGuid,
            EntityGuid = entityGuid,
            HasHost = hasHost,
            StructurallyValid = structurallyValid,
            // Content-valid = structurally valid AND carries real decoded content: a non-empty stack, a real
            // resolved frame name, and a service.name identifying the entity.
            ContentValid = structurallyValid && maxFramesInAnySample > 0 && hasRealFrameName && hasServiceName,
        };
    }
}
