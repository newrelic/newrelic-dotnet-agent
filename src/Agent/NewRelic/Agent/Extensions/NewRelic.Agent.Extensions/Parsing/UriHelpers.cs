// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


namespace NewRelic.Agent.Extensions.Parsing
{
    public static class UriHelpers
    {
        public static string GetTransactionNameFromPath(string path)
        {
            if (path.StartsWith("/"))
                path = path.Substring(1);

            if (path == string.Empty)
                path = "Root";

            return path;
        }
    }
}
