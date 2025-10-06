// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Helpers;
using System.Collections.Generic;
using System.Data.Common;
using System;
using NewRelic.Agent.Extensions.Logging;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public class OdbcConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "server", "data source", "hostname" };
        private static readonly List<string> _portKeys = new List<string> { "port" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public OdbcConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ParseHost();
            if (host != null) host = ConnectionStringParserHelper.NormalizeHostname(host, utilizationHostName);
            var portPathOrId = ParsePortPathOrId();
            var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;
            var instanceName = ParseInstanceName();
            return new ConnectionInfo(host, portPathOrId, databaseName, instanceName);
        }

        private string ParseHost()
        {
            try
            {
                var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (host == null) return null;

                // Handle MSSQL-style patterns possibly present in ODBC:
                // host,1234\instance
                // host\instance,1234
                var commaIndex = host.IndexOf(StringSeparators.CommaChar);
                var backslashIndex = host.IndexOf(StringSeparators.BackslashChar);

                int splitIndex;
                if (commaIndex == -1 && backslashIndex == -1)
                    splitIndex = -1;
                else if (commaIndex == -1)
                    splitIndex = backslashIndex;
                else if (backslashIndex == -1)
                    splitIndex = commaIndex;
                else
                    splitIndex = Math.Min(commaIndex, backslashIndex);

                host = splitIndex == -1 ? host : host.Substring(0, splitIndex);
                var endOfHostname = host.IndexOf(StringSeparators.ColonChar);
                return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in OdbcConnectionStringParser.ParseHost");
                return null;
            }
        }

        private string ParsePortPathOrId()
        {
            try
            {
                // Prefer explicit "port" key
                var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
                if (!string.IsNullOrWhiteSpace(portPathOrId))
                {
                    return portPathOrId;
                }

                portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (portPathOrId == null) return null;

                // host:1234 pattern
                var colonIndex = portPathOrId.IndexOf(StringSeparators.ColonChar);
                if (colonIndex != -1)
                {
                    var startOfValue = colonIndex + 1;
                    if (startOfValue < portPathOrId.Length)
                        return portPathOrId.Substring(startOfValue);
                }

                // Comma patterns, handling both host,port\instance and host\instance,port
                var commaIndex = portPathOrId.LastIndexOf(StringSeparators.CommaChar);
                if (commaIndex != -1)
                {
                    var start = commaIndex + 1;
                    if (start >= portPathOrId.Length)
                        return null;

                    var firstBackslash = portPathOrId.IndexOf(StringSeparators.BackslashChar);
                    var backslashAfterComma = portPathOrId.IndexOf(StringSeparators.BackslashChar, start);
                    var useBackslash = backslashAfterComma;

                    if (firstBackslash != -1 && firstBackslash < commaIndex)
                    {
                        // Pattern: host\instance,port
                        useBackslash = -1;
                    }

                    var end = useBackslash == -1 ? portPathOrId.Length : useBackslash;
                    if (end <= start) return null;

                    var port = portPathOrId.Substring(start, end - start).Trim();
                    return string.IsNullOrEmpty(port) ? null : port;
                }
                return "default";
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in OdbcConnectionStringParser.ParsePortPathOrId");
                return null;
            }
        }

        private string ParseInstanceName()
        {
            try
            {
                var instanceName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (instanceName == null) return null;

                // Support patterns:
                //  host\instance
                //  host\instance,port
                //  host,port\instance
                // Extract token after last backslash, before any comma (port delimiter).
                var lastBackslash = instanceName.LastIndexOf(StringSeparators.BackslashChar);
                if (lastBackslash == -1 || lastBackslash == instanceName.Length - 1)
                    return null;

                var start = lastBackslash + 1;
                var commaAfterInstance = instanceName.IndexOf(StringSeparators.CommaChar, start);
                var end = commaAfterInstance == -1 ? instanceName.Length : commaAfterInstance;

                if (end <= start)
                    return null;

                return instanceName.Substring(start, end - start);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in OdbcConnectionStringParser.ParseInstanceName");
                return null;
            }
        }
    }
}
