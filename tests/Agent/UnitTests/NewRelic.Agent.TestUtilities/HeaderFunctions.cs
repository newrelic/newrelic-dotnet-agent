// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.TestUtilities
{
    public static class HeaderFunctions
    {
        public static IList<string> GetHeaders(IEnumerable<KeyValuePair<string, string>> carrier, string key)
        {
            var headerValues = new List<string>();

            foreach (var item in carrier)
            {
                if (item.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                {
                    headerValues.Add(item.Value);
                }
            }

            return headerValues;

        }
    }
}
