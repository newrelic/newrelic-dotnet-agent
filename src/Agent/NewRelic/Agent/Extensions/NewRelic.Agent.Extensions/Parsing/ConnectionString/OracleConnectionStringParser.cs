// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public class OracleConnectionStringParser : IConnectionStringParser
    {
        private static readonly string _closeParen = new string(StringSeparators.CloseParen);
        private static readonly char[] _stopChars = { StringSeparators.ColonChar, StringSeparators.PathSeparatorChar };

        private static readonly List<string> _hostKeys = new List<string> { "server", "data source", "host", "dbq" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public OracleConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ParseHost();
            var portStr = ParsePortString();

            // If we could not extract any port token, treat as "default"
            if (string.IsNullOrEmpty(portStr))
            {
                return new ConnectionInfo(host, "default", null);
            }

            if (!int.TryParse(portStr, out var port))
            {
                // Non‑numeric port -> unknown
                port = -1;
            }

            return new ConnectionInfo(host, port, null);
        }

        private string ParseHost()
        {
            try
            {
                var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (host == null) return null;

                // Example patterns we must handle:
                // (DESCRIPTION=(ADDRESS_LIST=(ADDRESS=(PROTOCOL=TCP)(HOST=MyHost)(PORT=MyPort)))(CONNECT_DATA=(SERVICE_NAME=MyOracleSID)))
                // username/password@myserver:1521/myservice
                // username/password@//myserver:1521/my.service.com;
                // username/password@myserver/myservice
                // 111.21.31.99:1521/XE
                // serverName

                if (host.Contains(StringSeparators.OpenParenChar))
                {
                    var sections = host.Split(StringSeparators.OpenParen);
                    foreach (var section in sections)
                    {
                        if (!section.ToLowerInvariant().Contains("host=")) continue;
                        var startOfValue = section.IndexOf(StringSeparators.EqualSignChar) + 1;
                        if (startOfValue <= 0 || startOfValue >= section.Length) continue;
                        return section.Substring(startOfValue).Replace(_closeParen, string.Empty);
                    }
                    return null;
                }

                if (host.Contains(StringSeparators.AtSignChar))
                {
                    // Take everything after the first '@'
                    var atIndex = host.IndexOf(StringSeparators.AtSignChar);
                    if (atIndex == -1 || atIndex + 1 >= host.Length) return null;

                    var remainder = host.Substring(atIndex + 1);

                    // Optional // prefix
                    if (remainder.StartsWith("//"))
                        remainder = remainder.Substring(2);

                    // Host[:port] is up to the first '/' (if any)
                    var slashIndex = remainder.IndexOf(StringSeparators.PathSeparatorChar);
                    var hostPortSegment = slashIndex == -1 ? remainder : remainder.Substring(0, slashIndex);
                    if (string.IsNullOrWhiteSpace(hostPortSegment)) return null;

                    var colonIndex = hostPortSegment.IndexOf(StringSeparators.ColonChar);
                    return colonIndex == -1 ? hostPortSegment : hostPortSegment.Substring(0, colonIndex);
                }

                // Simple host[:port][/service]
                var endOfHostname = host.IndexOfAny(_stopChars);
                return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in OracleConnectionStringParser.ParseHost");
                return null;
            }
        }

        private string ParsePortString()
        {
            try
            {
                var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (host == null) return null;

                if (host.Contains(StringSeparators.OpenParenChar))
                {
                    // (DESCRIPTION=...) style
                    var sections = host.Split(StringSeparators.OpenParen);
                    foreach (var section in sections)
                    {
                        if (!section.ToLowerInvariant().Contains("port=")) continue;
                        var startOfValue1 = section.IndexOf(StringSeparators.EqualSignChar) + 1;
                        if (startOfValue1 <= 0 || startOfValue1 >= section.Length) continue;
                        var portToken = section.Substring(startOfValue1).Replace(_closeParen, string.Empty);
                        return string.IsNullOrWhiteSpace(portToken) ? null : portToken;
                    }
                    return null;
                }

                if (host.Contains(StringSeparators.AtSignChar))
                {
                    // username/password@host[:port]/service (with optional leading //)
                    var atIndex = host.IndexOf(StringSeparators.AtSignChar);
                    if (atIndex == -1 || atIndex + 1 >= host.Length) return null;

                    var remainder = host.Substring(atIndex + 1);
                    if (remainder.StartsWith("//"))
                        remainder = remainder.Substring(2);

                    var slashIndex = remainder.IndexOf(StringSeparators.PathSeparatorChar);
                    var hostPortSegment = slashIndex == -1 ? remainder : remainder.Substring(0, slashIndex);
                    if (string.IsNullOrEmpty(hostPortSegment)) return null;

                    var colonIndex = hostPortSegment.IndexOf(StringSeparators.ColonChar);
                    if (colonIndex == -1) return null; // no explicit port -> default

                    if (colonIndex + 1 >= hostPortSegment.Length)
                    {
                        // Colon present but empty port portion -> treat as invalid (unknown)
                        return "invalid_port";
                    }

                    var portCandidate = hostPortSegment.Substring(colonIndex + 1);
                    return string.IsNullOrWhiteSpace(portCandidate) ? "invalid_port" : portCandidate;
                }

                // Simple host:port/service or host:port
                var colonSimple = host.IndexOf(StringSeparators.ColonChar);
                if (colonSimple == -1) return null;

                var startOfValue = colonSimple + 1;
                if (startOfValue >= host.Length) return "invalid_port";

                var endOfValue = host.IndexOf(StringSeparators.PathSeparatorChar, startOfValue);
                if (endOfValue == -1) endOfValue = host.Length;

                if (endOfValue <= startOfValue) return "invalid_port";

                var simplePort = host.Substring(startOfValue, endOfValue - startOfValue);
                return string.IsNullOrWhiteSpace(simplePort) ? "invalid_port" : simplePort;
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in OracleConnectionStringParser.ParsePortString");
                return null;
            }
        }
    }
}
