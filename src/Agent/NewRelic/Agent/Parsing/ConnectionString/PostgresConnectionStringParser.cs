// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System.Data.Common;

namespace NewRelic.Parsing.ConnectionString
{
    public class PostgresConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "host", "server", "data source" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database", "location" };
        private static readonly List<string> _portKeys = new List<string> { "port" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public PostgresConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;

            if (host != null)
                host = ConnectionStringParserHelper.NormalizeHostname(host, utilizationHostName);

            var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

            var port = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
            if (port == null || !int.TryParse(port, out int portNum))
            {
                portNum = -1;
            }

            return new ConnectionInfo(DatastoreVendor.Postgres.ToKnownName(), host, portNum, databaseName);
        }
    }
}
