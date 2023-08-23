using MySqlConnector;
using NewRelic.Agent.IntegrationTests.Shared;

namespace NewRelic.Agent.UnboundedIntegrationTests.MySql
{
    public static class MsSqlWarmupHelper
    {
        public static void WarmupMySql()
        {
            using var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString);
            using var mySqlCommand = new MySqlCommand("SELECT _date FROM dates LIMIT 1", connection);
            connection.Open();
            mySqlCommand.ExecuteScalar();
        }

    }
}
