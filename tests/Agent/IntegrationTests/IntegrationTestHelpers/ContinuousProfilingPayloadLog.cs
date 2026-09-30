// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.IntegrationTestHelpers;

public static class ContinuousProfilingPayloadLog
{
    // The collector-method name ProfilesTransport logs the payload under.
    public const string CollectorMethodName = "continuous_profiling";

    private const string EncodedPayloadPrefix = "gzip+base64:";
    private const string UnavailablePlaceholderPrefix = "{json not available by default";
    private const string TooLargePlaceholderPrefix = "{payload too large";

    // Finest line written by ProfilesTransport.Send. With NEW_RELIC_PROFILING_LOG_PAYLOAD=true the value is
    // "gzip+base64:<b64>"; without it (or when too large) the value is a "{...}" placeholder, which this
    // regex deliberately does not match. Group 1 = the base64 payload.
    public static readonly string LogLineRegex =
        AgentLogBase.FinestLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : gzip\+base64:([A-Za-z0-9+/=]+)\s*$";

    // Matches the default (switch off) placeholder line, for negative assertions.
    public static readonly string PlaceholderLogLineRegex =
        AgentLogBase.FinestLogLinePrefixRegex + @"Request\(.+?\): Invoked ""continuous_profiling"" with : " + Regex.Escape(UnavailablePlaceholderPrefix);

    // Turns the logged payload value into JSON. Returns false only for the two known "{...}" placeholder
    // shapes (nothing to validate). Anything else -- including an empty or garbled value -- is decoded and so
    // throws (bad base64 / gzip), or returns JSON the caller parses and rejects, so it is never skipped silently.
    public static bool TryDecodeLoggedPayload(string loggedValue, out string json)
    {
        json = null;
        if (loggedValue.StartsWith(UnavailablePlaceholderPrefix, StringComparison.Ordinal) ||
            loggedValue.StartsWith(TooLargePlaceholderPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var base64 = loggedValue.StartsWith(EncodedPayloadPrefix, StringComparison.Ordinal)
            ? loggedValue.Substring(EncodedPayloadPrefix.Length)
            : loggedValue;

        json = DecodeToJson(base64.Trim());
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
