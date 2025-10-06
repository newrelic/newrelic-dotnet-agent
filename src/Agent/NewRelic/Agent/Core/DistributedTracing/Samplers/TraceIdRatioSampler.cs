// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DistributedTracing.Samplers;

// based on the OpenTelemetry TraceIdRatioSampler https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/Trace/Sampler/TraceIdRatioBasedSampler.cs
public class TraceIdRatioSampler : ISampler
{
    private readonly long _idUpperBound;
    private const int TraceIdLength = 16;

    private const float PriorityBoost = 1.0f;

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

        return new SamplingResult(sampled, sampled ? TracePriorityManager.Adjust(samplingParameters.Priority, PriorityBoost) : samplingParameters.Priority);
    }

    public void StartTransaction()
    {
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static long GetLowerLong(ReadOnlySpan<char> hex)
    {
        long result = 0;
        for (int i = 0; i < hex.Length; i++)
        {
            var v = hex[i] < (uint)HexLookup.Length ? HexLookup[hex[i]] : -1;
            if (v < 0)
                throw new FormatException("Trace ID contains invalid hexadecimal characters.");
            result = (result << 4) | (uint)v;
        }
        return result;
    }

    private static readonly sbyte[] HexLookup = CreateHexLookup();

    private static sbyte[] CreateHexLookup()
    {
        var arr = new sbyte['f' + 1]; // covers all valid ASCII hex chars
        for (int i = 0; i < arr.Length; i++) arr[i] = -1;
        for (char ch = '0'; ch <= '9'; ch++) arr[ch] = (sbyte)(ch - '0');
        for (char ch = 'a'; ch <= 'f'; ch++) arr[ch] = (sbyte)(ch - 'a' + 10);
        for (char ch = 'A'; ch <= 'F'; ch++) arr[ch] = (sbyte)(ch - 'A' + 10);
        return arr;
    }


}
