// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.Helpers;

public static class DictionaryHelpers
{
    /// <summary>
    /// Converts a JSON string to a case-insensitive dictionary.  Will always return a dictionary, even if the JSON is invalid.
    /// </summary>
    /// <param name="json"></param>
    /// <returns>IReadOnlyDictionary{string, object}</returns>
    public static IReadOnlyDictionary<string, object> FromJson(string json)
    {
        var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(json))
        {
            return result;
        }

        try
        {
            JsonConvert.PopulateObject(json, result);
        }
        catch
        {
            // invalid JSON: return an empty dictionary, not a partially populated one
            result.Clear();
        }

        return result;
    }
}
