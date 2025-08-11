// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

// based on the OpenTelemetry TraceIdRatioSampler https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/Trace/Sampler/TraceIdRatioBasedSampler.cs
public class TraceIdRatioSampler : ISampler
{
    private readonly long _idUpperBound;
    private const int TraceIdLength = 16;

    public TraceIdRatioSampler(float sampleRatio)
    {
        _idUpperBound = sampleRatio switch
        {
            // Special case the limits, to avoid any possible issues with lack of precision across
            // double/long boundaries. For probability == 0.0, we use Long.MIN_VALUE as this guarantees
            // that we will never sample a trace, even in the case where the id == Long.MIN_VALUE, since
            // Math.Abs(Long.MIN_VALUE) == Long.MIN_VALUE.
            0.0f => long.MinValue,
            1.0f => long.MaxValue,
            _ => (long)(sampleRatio * long.MaxValue)
        };
    }

    public ISamplingResult ShouldSample(ISamplingParameters samplingParameters)
    {
        if (string.IsNullOrEmpty(samplingParameters.TraceId))
        {
            throw new ArgumentNullException(nameof(samplingParameters.TraceId), "Trace ID cannot be null or empty.");
        }
        if (samplingParameters.TraceId.Length < TraceIdLength)
        {
            throw new FormatException($"Trace ID must be at least {TraceIdLength} characters long.");
        }

        // Note use of '<' for comparison. This ensures that we never sample for probability == 0.0,
        // while allowing for a (very) small chance of *not* sampling if the id == Long.MAX_VALUE.
        // This is considered a reasonable trade-off for the simplicity/performance requirements.
        var sampled = Math.Abs(GetLowerLong(samplingParameters.TraceId.AsSpan(0, TraceIdLength))) < _idUpperBound;

        return new SamplingResult(sampled, sampled ? BoostPriority(samplingParameters.Priority) : samplingParameters.Priority);
    }

    public void StartTransaction()
    {
    }

    private const float PriorityBoost = 1.0f;

    private static float BoostPriority(float priority)
    {
        return TracePriorityManager.Adjust(priority, PriorityBoost);
    }

    private static long GetLowerLong(ReadOnlySpan<char> hex)
    {
        long result = 0;
        foreach (var t in hex)
        {
            var value = ToHexValue(t);
            result = (result << 4) | (uint)value;
        }

        return result;
    }

    private static int ToHexValue(char c)
    {
        var value = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1
        };

        if (value == -1)
        {
            throw new FormatException("Trace ID contains invalid hexadecimal characters.");
        }

        return value;
    }
}
