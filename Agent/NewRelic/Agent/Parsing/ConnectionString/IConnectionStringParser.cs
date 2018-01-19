using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Parsing.ConnectionString
{
	public interface IConnectionStringParser
	{
		ConnectionInfo GetConnectionInfo();
	}

	public static class ConnectionInfoParser
	{
		private readonly static ConnectionInfo Empty = new ConnectionInfo(null, null, null);

		public static ConnectionInfo FromConnectionString(DatastoreVendor vendor, string connectionString)
		{
			IConnectionStringParser parser = GetConnectionParser(vendor, connectionString);

			return parser?.GetConnectionInfo() ?? Empty;
		}

		private static IConnectionStringParser GetConnectionParser(DatastoreVendor vendor, string connectionString)
		{
			switch (vendor)
			{
				case DatastoreVendor.MSSQL:
					return new MsSqlConnectionStringParser(connectionString);
				case DatastoreVendor.MySQL:
					return new MySqlConnectionStringParser(connectionString);
				case DatastoreVendor.Postgres:
					return new PostgresConnectionStringParser(connectionString);
				case DatastoreVendor.Oracle:
					return new OracleConnectionStringParser(connectionString);
				case DatastoreVendor.IBMDB2:
					return new IbmDb2ConnectionStringParser(connectionString);
				case DatastoreVendor.Redis:
					return new StackExchangeRedisConnectionStringParser(connectionString);
				default:
					return null;
			}
		}
	}
}