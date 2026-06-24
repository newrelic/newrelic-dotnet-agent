// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
#if NETFRAMEWORK
using System.Net;
#endif

namespace NewRelic.Agent.Core.DataTransport.Client;

/// <summary>
/// Formats HTTP response headers into a single-line string for debug logging.
/// </summary>
public static class HttpResponseHeaderFormatter
{
    private const string NoHeaders = "(none)";

    public static string Format(IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        if (headers == null)
        {
            return NoHeaders;
        }

        var formatted = headers
            .Select(header => $"{header.Key}=[{string.Join(", ", header.Value)}]")
            .ToList();

        return formatted.Count == 0 ? NoHeaders : string.Join("; ", formatted);
    }

#if NETFRAMEWORK
    public static string Format(WebHeaderCollection headers)
    {
        if (headers == null)
        {
            return NoHeaders;
        }

        var keyValuePairs = headers.AllKeys
            .Select(key => new KeyValuePair<string, IEnumerable<string>>(key, headers.GetValues(key) ?? new string[0]));

        return Format(keyValuePairs);
    }
#endif
}
