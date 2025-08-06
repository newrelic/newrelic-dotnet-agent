// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;

namespace NewRelic.Agent.Core.DistributedTracing
{
    // based on the OpenTelemetry TraceIdRatioSampler https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/Trace/Sampler/TraceIdRatioBasedSampler.cs
    public class TraceIdRatioSampler
    {
        private readonly long _idUpperBound;

        public TraceIdRatioSampler(double sampleRatio)
        {
            _idUpperBound = sampleRatio switch
            {
                // Special case the limits, to avoid any possible issues with lack of precision across
                // double/long boundaries. For probability == 0.0, we use Long.MIN_VALUE as this guarantees
                // that we will never sample a trace, even in the case where the id == Long.MIN_VALUE, since
                // Math.Abs(Long.MIN_VALUE) == Long.MIN_VALUE.
                0.0 => long.MinValue,
                1.0 => long.MaxValue,
                _ => (long)(sampleRatio * long.MaxValue)
            };
        }

        public bool ShouldSample(string traceId)
        {
            if (string.IsNullOrEmpty(traceId))
            {
                throw new ArgumentNullException(nameof(traceId), "Trace ID cannot be null or empty.");
            }
            if (traceId.Length < 16)
            {
                throw new FormatException("Trace ID must be at least 16 characters long.");
            }

            // Note use of '<' for comparison. This ensures that we never sample for probability == 0.0,
            // while allowing for a (very) small chance of *not* sampling if the id == Long.MAX_VALUE.
            // This is considered a reasonable trade-off for the simplicity/performance requirements.
            return Math.Abs(GetLowerLong(traceId.AsSpan(0, 16))) < _idUpperBound;
        }

        private static long GetLowerLong(ReadOnlySpan<char> hex)
        {
            long result = 0;
            for (var i = 0; i < 16; i++)
            {
                var value = ToHexValue(hex[i]);

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
}
