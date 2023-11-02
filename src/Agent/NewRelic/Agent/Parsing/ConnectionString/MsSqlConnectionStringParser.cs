// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace NewRelic.Parsing.ConnectionString
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
            return new ConnectionInfo(DatastoreVendor.MySQL.ToKnownName(), host, portPathOrId, databaseName, instanceName);
        }

        private string ParseHost()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            // Example of want we would need to process: win-database.pdx.vm.datanerd.us,1433\SQLEXPRESS
            try
            {
                var splitIndex = host.IndexOf(',');
                if (splitIndex == -1) splitIndex = host.IndexOf('\\');
                host = splitIndex == -1 ? host : host.Substring(0, splitIndex);
            }
            catch
            {
                return null;
            }
            return host;
        }

        private string ParsePortPathOrId()
        {
            var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (portPathOrId == null) return null;

            try
            {
                if (portPathOrId.IndexOf(',') != -1)
                {
                    var startOfValue = portPathOrId.IndexOf(',') + 1;
                    var endOfValue = portPathOrId.Contains('\\')
                        ? portPathOrId.IndexOf('\\')
                        : portPathOrId.Length;
                    return (startOfValue > 0) ? portPathOrId.Substring(startOfValue, endOfValue - startOfValue) : null;
                }
            }
            catch
            {
                return null;
            }

            return "default";
        }

        private string ParseInstanceName()
        {
            var instanceName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (instanceName == null) return null;

            try
            {
                if (instanceName.IndexOf('\\') != -1)
                {
                    var startOfValue = instanceName.IndexOf('\\') + 1;
                    var endOfValue = instanceName.Length;
                    return (startOfValue > 0) ? instanceName.Substring(startOfValue, endOfValue - startOfValue) : null;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }
    }
}
