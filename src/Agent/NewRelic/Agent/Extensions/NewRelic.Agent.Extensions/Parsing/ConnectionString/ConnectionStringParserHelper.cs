// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public static class ConnectionStringParserHelper
    {
        public static KeyValuePair<string, string>? GetKeyValuePair(DbConnectionStringBuilder connectionStringBuilder, List<string> possibleKeys)
        {
            var key = possibleKeys.FirstOrDefault(k => connectionStringBuilder.ContainsKey(k));
            if (key == null) return null;
            var value = connectionStringBuilder[key].ToString();
            return new KeyValuePair<string, string>(key, value);
        }

        public static string NormalizeHostname(string host, string utilizationHostName)
        {
            var localhost = new[] { ".", "localhost" };
            var hostIsLocalhost = localhost.Contains(host);
            if (!hostIsLocalhost)
            {
                var isIpAddress = IPAddress.TryParse(host, out var ipAddress);
                hostIsLocalhost = isIpAddress && IPAddress.IsLoopback(ipAddress);
            }

            var resolvedHostName = hostIsLocalhost ? utilizationHostName : host;
            return resolvedHostName;
        }
    }
}
