// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data.Common;
using NewRelic.Agent.Extensions.Logging;
using NewRelic.Agent.Helpers;

namespace NewRelic.Agent.Extensions.Parsing.ConnectionString
{
    public class MySqlConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "network address", "server", "data source" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database" };
        private static readonly List<string> _portKeys = new List<string> { "port" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public MySqlConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            try
            {
                var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;

                var hasMultipleHosts = host != null && host.IndexOf(StringSeparators.CommaChar) != -1;
                if (hasMultipleHosts)
                    host = null;
                else if (host != null)
                    host = ConnectionStringParserHelper.NormalizeHostname(host, utilizationHostName);

                var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

                var port = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
                if (port == null && host != null)
                {
                    return new ConnectionInfo(host, "default", databaseName);
                }
                else
                {
                    int portNum;
                    if (!int.TryParse(port, out portNum))
                    {
                        portNum = -1;
                    }
                    return new ConnectionInfo(host, portNum, databaseName);
                }
            }
            catch (Exception e)
            {

                Log.Debug(e, "Unhandled exception in MySqlConnectionStringParser.GetConnectionInfo");
                return new ConnectionInfo(null, null, null);
            }
        }
    }
}
