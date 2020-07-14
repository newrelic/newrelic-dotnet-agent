using System;
using System.Collections.Generic;
using System.Data.Common;

namespace NewRelic.Parsing.ConnectionString
{
	public class PostgresConnectionStringParser : IConnectionStringParser
	{
		private static readonly List<String> _hostKeys = new List<String> { "host", "server", "data source" };
		private static readonly List<String> _databaseNameKeys = new List<String> { "database", "location" };
		private static readonly List<String> _portKeys = new List<String> { "port" };

		private readonly DbConnectionStringBuilder _connectionStringBuilder;

		public PostgresConnectionStringParser(String connectionString)
		{
			_connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
		}

		public ConnectionInfo GetConnectionInfo()
		{
			var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;

			if (host != null)
				host = ConnectionStringParserHelper.NormalizeHostname(host);

			var portPathOrId = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _portKeys)?.Value;
			var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

			return new ConnectionInfo(host, portPathOrId, databaseName);
		}
	}
}
