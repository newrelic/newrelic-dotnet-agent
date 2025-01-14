// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.Helpers;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

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
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            // Example of want we would need to process: win-database.pdx.vm.datanerd.us,1433\SQLEXPRESS
            try
            {
                var splitIndex = host.IndexOf(StringSeparators.CommaChar);
                if (splitIndex == -1) splitIndex = host.IndexOf(StringSeparators.BackslashChar);
                host = splitIndex == -1 ? host : host.Substring(0, splitIndex);
            }
            catch
            {
                return null;
            }
            var endOfHostname = host.IndexOf(StringSeparators.ColonChar);
            return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
        }

        private string ParsePortPathOrId()
        {
            // Some ODBC drivers use the "port" key to specify the port number
            var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
            if (!string.IsNullOrWhiteSpace(portPathOrId))
            {
                return portPathOrId;

            }

            // Some ODBC drivers include the port in the "server" or "data source" key
            portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (portPathOrId == null) return null;

            try
            {
                if (portPathOrId.IndexOf(StringSeparators.ColonChar) != -1)
                {
                    var startOfValue = portPathOrId.IndexOf(StringSeparators.ColonChar) + 1;
                    var endOfValue = portPathOrId.Length;
                    return (startOfValue > 0) ? portPathOrId.Substring(startOfValue, endOfValue - startOfValue) : null;
                }
                if (portPathOrId.IndexOf(StringSeparators.CommaChar) != -1)
                {
                    var startOfValue = portPathOrId.IndexOf(StringSeparators.CommaChar) + 1;
                    var endOfValue = portPathOrId.Contains(StringSeparators.BackslashChar)
                        ? portPathOrId.IndexOf(StringSeparators.BackslashChar)
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
                if (instanceName.IndexOf(StringSeparators.BackslashChar) != -1)
                {
                    var startOfValue = instanceName.IndexOf(StringSeparators.BackslashChar) + 1;
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
