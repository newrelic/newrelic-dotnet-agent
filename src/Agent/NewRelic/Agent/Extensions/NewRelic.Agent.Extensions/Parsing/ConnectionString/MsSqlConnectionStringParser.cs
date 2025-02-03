// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
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
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            // Example of want we would need to process: win-database.pdx.vm.datanerd.us,1433\SQLEXPRESS
            var splitIndex = host.IndexOf(StringSeparators.CommaChar);
            if (splitIndex == -1) splitIndex = host.IndexOf(StringSeparators.BackslashChar);
            host = splitIndex == -1 ? host : host.Substring(0, splitIndex);
            return host;
        }

        private string ParsePortPathOrId()
        {
            var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (portPathOrId == null) return null;

            if (portPathOrId.IndexOf(StringSeparators.CommaChar) != -1)
            {
                var startOfValue = portPathOrId.IndexOf(StringSeparators.CommaChar) + 1;
                var endOfValue = portPathOrId.Contains(StringSeparators.BackslashChar)
                    ? portPathOrId.IndexOf(StringSeparators.BackslashChar)
                    : portPathOrId.Length;
                return portPathOrId.Substring(startOfValue, endOfValue - startOfValue);
            }

            return "default";
        }

        private string ParseInstanceName()
        {
            var instanceName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (instanceName == null) return null;

            if (instanceName.IndexOf(StringSeparators.BackslashChar) != -1)
            {
                var startOfValue = instanceName.IndexOf(StringSeparators.BackslashChar) + 1;
                var endOfValue = instanceName.Length;
                return instanceName.Substring(startOfValue, endOfValue - startOfValue);
            }

            return null;
        }
    }
}
