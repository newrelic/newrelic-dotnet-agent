// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MockNewRelic.Models;
using Opentelemetry.Proto.Collector.Metrics.V1;
using Opentelemetry.Proto.Common.V1;
using Opentelemetry.Proto.Metrics.V1;

namespace MockNewRelic.Controllers;

[ApiController]
[Route("v1/metrics")]
public class OtlpMetricsController : ControllerBase
{
    private const int MaxStoredSummaries = 100;
    private static readonly ConcurrentQueue<MetricsSummaryDto> _summaries = new ConcurrentQueue<MetricsSummaryDto>();

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        // Expect application/x-protobuf
        var contentType = Request.ContentType?.ToLowerInvariant();
        if (contentType == null || !contentType.Contains("application/x-protobuf"))
        {
            // bad content type
            return StatusCode(415, "{}");
        }

        byte[] bodyBytes = await ReadRequestBodyAsync(Request);

        ExportMetricsServiceRequest request;
        try
        {
            request = ExportMetricsServiceRequest.Parser.ParseFrom(bodyBytes);
        }
        catch
        {
            // Bad payload
            return StatusCode(400, "{}");
        }

        var summary = Summarize(request);
        _summaries.Enqueue(summary);

        // enforce max capacity
        while (_summaries.Count > MaxStoredSummaries && _summaries.TryDequeue(out _)) { }

        // Return empty JSON with 200
        return Ok("{}");
    }

    /// <summary>
    /// Get the most recent collected metrics summaries.
    /// </summary>
    /// <param name="n">The number of summaries to retrieve, default is 10.</param>
    /// <returns></returns>
    [HttpGet("collected")]
    public IActionResult GetCollected([FromQuery] int n = 10)
    {
        if (n <= 0) n = 1;
        var items = _summaries.Reverse().Take(n).ToList();
        return Ok(items);
    }

    /// <summary>
    /// Get the count of stored metrics summaries.
    /// </summary>
    /// <returns></returns>
    [HttpGet("count")]
    public IActionResult GetCount()
    {
        return Ok(_summaries.Count);
    }

    /// <summary>
    /// Clear all stored metrics summaries.
    /// </summary>
    /// <returns></returns>
    [HttpPost("clear")]
    public IActionResult Clear()
    {
        while (_summaries.TryDequeue(out _)) { }
        return Ok("{}");
    }

    private static async Task<byte[]> ReadRequestBodyAsync(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        using var ms = new MemoryStream();
        if (request.Body == null)
        {
            return Array.Empty<byte>();
        }

        // Handle content-encoding gzip/deflate
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

    private static MetricsSummaryDto Summarize(ExportMetricsServiceRequest request)
    {
        var summary = new MetricsSummaryDto
        {
            ReceivedAtUtc = DateTime.UtcNow,
        };

        int totalMetricCount = 0;
        int totalDataPointCount = 0;

        foreach (var rm in request.ResourceMetrics)
        {
            var resourceSummary = new ResourceSummary();

            if (rm.Resource != null)
            {
                foreach (var attr in rm.Resource.Attributes)
                {
                    resourceSummary.Attributes[attr.Key] = AttributeToString(attr.Value);
                }
            }

            foreach (var sm in rm.ScopeMetrics)
            {
                var scopeSummary = new ScopeSummary
                {
                    Name = sm.Scope?.Name ?? string.Empty,
                    Version = sm.Scope?.Version ?? string.Empty,
                };

                foreach (var metric in sm.Metrics)
                {
                    totalMetricCount++;
                    var metricSummary = new MetricSummary
                    {
                        Name = metric.Name,
                        Type = metric.DataCase.ToString(),
                        DataPointCount = CountDataPoints(metric)
                    };
                    totalDataPointCount += metricSummary.DataPointCount;
                    scopeSummary.Metrics.Add(metricSummary);
                }

                resourceSummary.Scopes.Add(scopeSummary);
            }

            summary.Resources.Add(resourceSummary);
        }

        summary.TotalMetricCount = totalMetricCount;
        summary.TotalDataPointCount = totalDataPointCount;
        return summary;
    }

    private static int CountDataPoints(Metric metric)
    {
        return metric.DataCase switch
        {
            Metric.DataOneofCase.Gauge => metric.Gauge?.DataPoints?.Count ?? 0,
            Metric.DataOneofCase.Sum => metric.Sum?.DataPoints?.Count ?? 0,
            Metric.DataOneofCase.Histogram => metric.Histogram?.DataPoints?.Count ?? 0,
            Metric.DataOneofCase.ExponentialHistogram => metric.ExponentialHistogram?.DataPoints?.Count ?? 0,
            Metric.DataOneofCase.Summary => metric.Summary?.DataPoints?.Count ?? 0,
            _ => 0
        };
    }

    private static string AttributeToString(AnyValue value)
    {
        switch (value.ValueCase)
        {
            case AnyValue.ValueOneofCase.StringValue: return value.StringValue;
            case AnyValue.ValueOneofCase.BoolValue: return value.BoolValue.ToString();
            case AnyValue.ValueOneofCase.IntValue: return value.IntValue.ToString();
            case AnyValue.ValueOneofCase.DoubleValue: return value.DoubleValue.ToString();
            case AnyValue.ValueOneofCase.ArrayValue: return $"[len={value.ArrayValue?.Values?.Count ?? 0}]";
            case AnyValue.ValueOneofCase.KvlistValue:
                var kvs = value.KvlistValue?.Values?.Select(kv => $"{kv.Key}={AttributeToString(kv.Value)}") ?? Enumerable.Empty<string>();
                return string.Join(",", kvs);
            default: return string.Empty;
        }
    }
}
