// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace NewRelic.Parsing.ConnectionString
{
    public class IbmDb2ConnectionStringParser : IConnectionStringParser
    {
        private static readonly List<string> _hostKeys = new List<string> { "network address", "server", "hostname" };
        private static readonly List<string> _databaseNameKeys = new List<string> { "database" };

        private readonly DbConnectionStringBuilder _connectionStringBuilder;

        public IbmDb2ConnectionStringParser(string connectionString)
        {
            _connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }

        public ConnectionInfo GetConnectionInfo(string utilizationHostName)
        {
            var host = ParseHost();
            if (host != null) host = ConnectionStringParserHelper.NormalizeHostname(host, utilizationHostName);
            var portPathOrId = ParsePortPathOrId();
            var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

            return new ConnectionInfo(DatastoreVendor.IBMDB2.ToKnownName(), host, portPathOrId, databaseName);
        }

        private string ParseHost()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            var endOfHostname = host.IndexOf(':');
            return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
        }

        private string ParsePortPathOrId()
        {
            var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
            if (host == null) return null;

            if (host.Contains(':'))
                return host.Substring(host.IndexOf(':') + 1);

            return "default";
        }
    }
}
