// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewRelic.Agent.Extensions.Helpers
{
    public static class DictionaryHelpers
    {
        /// <summary>
        /// Converts a JSON string to a dictionary.  Will always return a dictionary, even if the JSON is invalid.
        /// </summary>
        /// <param name="json"></param>
        /// <returns>IReadOnlyDictionary<string, object></returns>
        public static IReadOnlyDictionary<string, object> FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, object>();
            }

            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }
    }
}
