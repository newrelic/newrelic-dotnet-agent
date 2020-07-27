using System;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Parsing.ConnectionString
{
    public class ConnectionInfo
    {
        public ConnectionInfo()
        {
        }

        public ConnectionInfo(String host, String portPathOrId, String databaseName, String instanceName = null)
        {
            Host = host;
            PortPathOrId = portPathOrId;
            DatabaseName = databaseName;
            InstanceName = instanceName;
        }

        public String Host { get; private set; }
        public String PortPathOrId { get; private set; }
        public String DatabaseName { get; private set; }
        public String InstanceName { get; private set; }

        public static ConnectionInfo FromConnectionString(DatastoreVendor vendor, String connectionString)
        {
            IConnectionStringParser parser;

            if (vendor == DatastoreVendor.MSSQL)
                parser = new MsSqlConnectionStringParser(connectionString);
            else if (vendor == DatastoreVendor.MySQL)
                parser = new MySqlConnectionStringParser(connectionString);
            else if (vendor == DatastoreVendor.Postgres)
                parser = new PostgresConnectionStringParser(connectionString);
            else if (vendor == DatastoreVendor.Oracle)
                parser = new OracleConnectionStringParser(connectionString);
            else if (vendor == DatastoreVendor.IBMDB2)
                parser = new IbmDb2ConnectionStringParser(connectionString);
            else if (vendor == DatastoreVendor.Redis)
                parser = new StackExchangeRedisConnectionStringParser(connectionString);
            else
                parser = null;

            var connectionInfo = parser != null ? parser.GetConnectionInfo() : new ConnectionInfo();
            return connectionInfo;
        }
    }
}
