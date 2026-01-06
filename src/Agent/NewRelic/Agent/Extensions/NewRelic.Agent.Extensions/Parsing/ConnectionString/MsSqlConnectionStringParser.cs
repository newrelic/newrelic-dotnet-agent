// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data.Common;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public class MsSqlConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "server", "data source" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database", "initial catalog" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public MsSqlConnectionStringParser(string connectionString)
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

                // Examples we need to process:
                // host,1433\SQLEXPRESS
                // host\SQLEXPRESS,1433
                // host,1433
                // host\SQLEXPRESS
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

                return splitIndex == -1 ? host : host.Substring(0, splitIndex);
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in MsSqlConnectionStringParser.ParseHost");
                return null;
            }
        }

        private string ParsePortPathOrId()
        {
            try
            {
                var value = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
                if (value == null)
                    return null;

                var commaIndex = value.LastIndexOf(StringSeparators.CommaChar);
                if (commaIndex == -1)
                    return "default"; // no explicit port supplied

                var start = commaIndex + 1;
                if (start >= value.Length)
                    return null; // malformed: trailing comma

                // Find a backslash AFTER the comma (instance name after port)
                var backslashAfterComma = value.IndexOf(StringSeparators.BackslashChar, start);

                // If there is a backslash before the comma (host\instance,port form), ignore that one and take entire suffix as port
                var firstBackslash = value.IndexOf(StringSeparators.BackslashChar);
                var useBackslash = backslashAfterComma;

                if (firstBackslash != -1 && firstBackslash < commaIndex)
                {
                    // Pattern: host\instance,port
                    useBackslash = -1; // port continues to end
                }

                var end = useBackslash == -1 ? value.Length : useBackslash;
                if (end <= start)
                    return null; // safety: malformed ordering

                var port = value.Substring(start, end - start).Trim();
                return string.IsNullOrEmpty(port) ? null : port;
            }
            catch (Exception e)
            {
                Log.Debug(e, "Unhandled exception in MsSqlConnectionStringParser.ParsePortPathOrId");
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
                // Take the substring after the LAST backslash, then trim any trailing comma+port portion.
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
                Log.Debug(e, "Unhandled exception in MsSqlConnectionStringParser.ParseInstanceName");
                return null;
            }
        }
    }
}
