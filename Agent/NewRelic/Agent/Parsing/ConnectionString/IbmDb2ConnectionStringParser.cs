using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

namespace NewRelic.Parsing.ConnectionString
{
	public class IbmDb2ConnectionStringParser : IConnectionStringParser
	{
		private static readonly List<String> _hostKeys = new List<String> { "network address", "server", "hostname" };
		private static readonly List<String> _databaseNameKeys = new List<String> { "database" };

		private readonly DbConnectionStringBuilder _connectionStringBuilder;

		public IbmDb2ConnectionStringParser(String connectionString)
		{
			_connectionStringBuilder = new DbConnectionStringBuilder { ConnectionString = connectionString };
		}

		public ConnectionInfo GetConnectionInfo()
		{
			var host = ParseHost();
			if (host != null) host = ConnectionStringParserHelper.NormalizeHostname(host);
			var portPathOrId = ParsePortPathOrId();
			var databaseName = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _databaseNameKeys)?.Value;

			return new ConnectionInfo(host, portPathOrId, databaseName);
		}

		private String ParseHost()
		{
			var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
			if (host == null) return null;

			var endOfHostname = host.IndexOf(':');
			return endOfHostname == -1 ? host : host.Substring(0, endOfHostname);
		}

		private String ParsePortPathOrId()
		{
			var host = ConnectionStringParserHelper.GetKeyValuePair(_connectionStringBuilder, _hostKeys)?.Value;
			if (host == null) return null;

			if (host.Contains(':'))
				return host.Substring(host.IndexOf(':') + 1);

			return "default";
		}
	}
}
