// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NewRelic.Agent.IntegrationTestHelpers;

public static class ContinuousProfilingPayloadLog
{
    // Finest line written by ProfilesTransport.Send. With NEW_RELIC_PROFILING_LOG_PAYLOAD=true the value is
    // "gzip+base64:<b64>"; without it (or when too large) the value is a "{...}" placeholder, which this
    // regex deliberately does not match. Group 1 = the base64 payload.
    public static readonly string LogLineRegex =
        AgentLogBase.FinestLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : gzip\+base64:([A-Za-z0-9+/=]+)\s*$";

    // Matches the default (switch off) placeholder line, for negative assertions.
    public static readonly string PlaceholderLogLineRegex =
        AgentLogBase.FinestLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : \{json not available by default";

    // The collector-method name ProfilesTransport logs the payload under.
    public const string CollectorMethodName = "continuous_profiling";

    private const string EncodedPayloadPrefix = "gzip+base64:";

    // Turns the logged payload value into JSON. Returns false for a "{...}" placeholder (nothing to validate);
    // throws if a "gzip+base64:" value does not decode, so a corrupt payload is still caught by the caller.
    public static bool TryDecodeLoggedPayload(string loggedValue, out string json)
    {
        json = null;
        if (!loggedValue.StartsWith(EncodedPayloadPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        json = DecodeToJson(loggedValue.Substring(EncodedPayloadPrefix.Length).Trim());
        return true;
    }

    public static string DecodeToJson(string base64)
    {
        using var input = new MemoryStream(Convert.FromBase64String(base64));
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
