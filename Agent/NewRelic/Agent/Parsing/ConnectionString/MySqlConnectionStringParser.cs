using NewRelic.Agent.Extensions.Parsing;
using System;
using System.Collections.Generic;
using System.Data.Common;

namespace NewRelic.Parsing.ConnectionString
{
	public class MySqlConnectionStringParser : IConnectionStringParser
	{
		private static readonly List<String> _hostKeys = new List<String> { "network address", "server", "data source" };
		private static readonly List<String> _databaseNameKeys = new List<String> { "database" };
		private static readonly List<String> _portKeys = new List<String> { "port" };

		private readonly DbConnectionStringBuilder _connectionStringBuilder;

		public MySqlConnectionStringParser(String connectionString)
		{
			_connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
		}

		public ConnectionInfo GetConnectionInfo()
		{
			var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;

			var hasMultipleHosts = host != null && host.IndexOf(',') != -1;
			if (hasMultipleHosts)
				host = null;
			else if (host != null)
				host = ConnectionStringParserHelper.NormalizeHostname(host);

			var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
			if (portPathOrId == null && host != null) portPathOrId = "default";

			var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

			return new ConnectionInfo(host, portPathOrId, databaseName);
		}
	}
}
