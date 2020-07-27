extern alias StackExchangeStrongNameAlias;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.Mvc;
using IBM.Data.DB2;
using Microsoft.Practices.EnterpriseLibrary.Common.Configuration;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using MySql.Data.MySqlClient;
using NewRelic.Agent.IntegrationTests.Shared;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace BasicMvcApplication.Controllers
{
    /// <remarks>
    /// StackExchange.Redis.StrongName has an alias of StackExchangeStrongNameAlias to avoid namespace collision with the standard assembly.
    /// extern alias StackExchangeStrongNameAlias; makes this usable in the file.
    /// </remarks>
    public class DefaultController : Controller
    {
        private const string InsertPersonMsSql = "INSERT INTO {0} (FirstName, LastName, Email) VALUES('Testy', 'McTesterson', 'testy@mctesterson.com')";
        private const string DeletePersonMsSql = "DELETE FROM {0} WHERE Email = 'testy@mctesterson.com'";
        private const string CountPersonMsSql = "SELECT COUNT(*) FROM {0} WITH(nolock)";

        private const string InsertHotelOracleSql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelOracleSql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelOracleSql = "SELECT COUNT(*) FROM {0}";

        private const string InsertHotelDB2Sql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
        private const string DeleteHotelDB2Sql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
        private const string CountHotelDB2Sql = "SELECT COUNT(*) FROM {0}";

        [HttpGet]
        public string MsSql(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = MsSqlConfiguration.MsSqlConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'Matthew'", connection))
                {

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (reader.NextResult())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = command.ExecuteScalar();
                }

                using (var command = new SqlCommand(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQuery();
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> MsSqlAsync(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = MsSqlConfiguration.MsSqlConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'Matthew'", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (await reader.NextResultAsync())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = await command.ExecuteScalarAsync();
                }

                using (var command = new SqlCommand(deleteSql, connection))
                {
                    var deleteCount = await command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }

        public string MsSql_WithParameterizedQuery(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = MsSqlConfiguration.MsSqlConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN", connection))
                {
                    command.Parameters.Add(new SqlParameter("@FN", "O'Keefe"));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (reader.NextResult())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = command.ExecuteScalar();
                }

                using (var command = new SqlCommand(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQuery();
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> MsSqlAsync_WithParameterizedQuery(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = MsSqlConfiguration.MsSqlConnectionString;

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();

                using (var command = new SqlCommand("SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN", connection))
                {
                    command.Parameters.Add(new SqlParameter("@FN", "O'Keefe"));
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            if (await reader.NextResultAsync())
                            {
                                teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                            }
                        }
                    }
                }

                var insertSql = string.Format(InsertPersonMsSql, tableName);
                var countSql = string.Format(CountPersonMsSql, tableName);
                var deleteSql = string.Format(DeletePersonMsSql, tableName);

                using (var command = new SqlCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new SqlCommand(countSql, connection))
                {
                    var teamMemberCount = await command.ExecuteScalarAsync();
                }

                using (var command = new SqlCommand(deleteSql, connection))
                {
                    var deleteCount = await command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public string MySql()
        {
            var teamMembers = new List<string>();

            var connectionString = MySqlTestConfiguration.MySqlConnectionString;

            using (var connection = new MySqlConnection(connectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 10000", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> MySqlAsync()
        {
            var teamMembers = new List<string>();

            var connectionString = MySqlTestConfiguration.MySqlConnectionString;

            using (var connection = new MySqlConnection(connectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 10000", connection))
            {
                connection.Open();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public string Oracle(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = OracleConfiguration.OracleConnectionString;

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();

                using (var command = new OracleCommand("SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("DEGREE")));
                        }
                    }
                }

                var insertSql = string.Format(InsertHotelOracleSql, tableName);
                var countSql = string.Format(CountHotelOracleSql, tableName);
                var deleteSql = string.Format(DeleteHotelOracleSql, tableName);

                using (var command = new OracleCommand(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new OracleCommand(countSql, connection))
                {
                    var hotelCount = command.ExecuteScalar();
                }

                using (var command = new OracleCommand(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQuery();
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> OracleAsync(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionString = OracleConfiguration.OracleConnectionString;

            using (var connection = new OracleConnection(connectionString))
            {
                connection.Open();

                using (var command = new OracleCommand("SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("DEGREE")));
                        }
                    }
                }

                var insertSql = string.Format(InsertHotelOracleSql, tableName);
                var countSql = string.Format(CountHotelOracleSql, tableName);
                var deleteSql = string.Format(DeleteHotelOracleSql, tableName);

                using (var command = new OracleCommand(insertSql, connection))
                {
                    var insertCount = await command.ExecuteNonQueryAsync();
                }

                using (var command = new OracleCommand(countSql, connection))
                {
                    var hotelCount = await command.ExecuteScalarAsync();
                }

                using (var command = new OracleCommand(deleteSql, connection))
                {
                    var deleteCount = await command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }
        [HttpGet]
        public string Postgres()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> PostgresAsync()
        {
            var teamMembers = new List<string>();

            var connectionString = PostgresConfiguration.PostgresConnectionString;

            using (var connection = new NpgsqlConnection(connectionString))
            using (var command = new NpgsqlCommand("SELECT * FROM newrelic.teammembers WHERE firstname = 'Matthew'", connection))
            {
                connection.Open();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            return string.Join(",", teamMembers);
        }


        [HttpGet]
        public string StackExchangeRedis()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;

            string value;

            using (var redis = StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                db.StringSet("mykey", "myvalue");
                value = db.StringGet("mykey");
            }

            return value;
        }


        [HttpGet]
        public string StackExchangeRedisStrongName()
        {
            var connectionString = StackExchangeRedisConfiguration.StackExchangeRedisConnectionString;

            string value;

            //Alias StrongName assembly to avoid type collisions
            using (var redis = StackExchangeStrongNameAlias::StackExchange.Redis.ConnectionMultiplexer.Connect(connectionString))
            {
                var db = redis.GetDatabase();
                db.StringSet("mykey", "myvalue");
                value = db.StringGet("mykey");
            }

            return value;
        }

        [HttpGet]
        public string EnterpriseLibraryOracle(string tableName)
        {
            var teamMembers = new List<string>();

            var connectionStringSettings = new ConnectionStringSettings("OracleConnection", OracleConfiguration.OracleConnectionString, "Oracle.ManagedDataAccess.Client");
            var connectionStringsSection = new ConnectionStringsSection();
            connectionStringsSection.ConnectionStrings.Add(connectionStringSettings);
            var dictionaryConfigSource = new DictionaryConfigurationSource();
            dictionaryConfigSource.Add("connectionStrings", connectionStringsSection);
            var dbProviderFactory = new DatabaseProviderFactory(dictionaryConfigSource);
            var oracleDatabase = dbProviderFactory.Create("OracleConnection");

            using (var reader = oracleDatabase.ExecuteReader(CommandType.Text, "SELECT DEGREE FROM user_tables WHERE ROWNUM <= 1"))
            {
                while (reader.Read())
                {
                    teamMembers.Add(reader.GetString(reader.GetOrdinal("DEGREE")));
                }
            }

            var insertSql = string.Format(InsertHotelOracleSql, tableName);
            var countSql = string.Format(CountHotelOracleSql, tableName);
            var deleteSql = string.Format(DeleteHotelOracleSql, tableName);

            var insertCount = oracleDatabase.ExecuteNonQuery(CommandType.Text, insertSql);
            var hotelCount = oracleDatabase.ExecuteScalar(CommandType.Text, countSql);
            var deleteCount = oracleDatabase.ExecuteNonQuery(CommandType.Text, deleteSql);

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public string EnterpriseLibraryMsSql(string tableName)
        {
            var teamMembers = new List<string>();

            //var msSqlDatabase = new DatabaseProviderFactory().Create("MSSQLConnection");
            var msSqlDatabase = new SqlDatabase(MsSqlConfiguration.MsSqlConnectionString);

            using (var reader = msSqlDatabase.ExecuteReader(CommandType.Text, "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'Matthew'"))
            {
                while (reader.Read())
                {
                    teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    if (reader.NextResult())
                    {
                        teamMembers.Add(reader.GetString(reader.GetOrdinal("FirstName")));
                    }
                }
            }

            var insertSql = string.Format(InsertPersonMsSql, tableName);
            var countSql = string.Format(CountPersonMsSql, tableName);
            var deleteSql = string.Format(DeletePersonMsSql, tableName);

            var insertCount = msSqlDatabase.ExecuteNonQuery(CommandType.Text, insertSql);
            var teamMemberCount = msSqlDatabase.ExecuteScalar(CommandType.Text, countSql);
            var deleteCount = msSqlDatabase.ExecuteNonQuery(CommandType.Text, deleteSql);

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public string InvokeIbmDb2Query(string tableName)
        {
            var teamMembers = new List<string>();

            using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
            {
                connection.Open();

                using (var command = new DB2Command("SELECT LASTNAME FROM EMPLOYEE FETCH FIRST ROW ONLY", connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("LASTNAME")));
                        }
                    }
                }

                var insertSql = string.Format(InsertHotelDB2Sql, tableName);
                var countSql = string.Format(CountHotelDB2Sql, tableName);
                var deleteSql = string.Format(DeleteHotelDB2Sql, tableName);

                using (var command = new DB2Command(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQuery();
                }

                using (var command = new DB2Command(countSql, connection))
                {
                    var hotelCount = command.ExecuteScalar();
                }

                using (var command = new DB2Command(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQuery();
                }
            }

            return string.Join(",", teamMembers);
        }

        [HttpGet]
        public async Task<string> InvokeIbmDb2QueryAsync(string tableName)
        {
            var teamMembers = new List<string>();

            using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
            {
                connection.Open();

                using (var command = new DB2Command("SELECT LASTNAME FROM EMPLOYEE FETCH FIRST ROW ONLY", connection))
                {
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            teamMembers.Add(reader.GetString(reader.GetOrdinal("LASTNAME")));
                        }
                    }
                }

                var insertSql = string.Format(InsertHotelDB2Sql, tableName);
                var countSql = string.Format(CountHotelDB2Sql, tableName);
                var deleteSql = string.Format(DeleteHotelDB2Sql, tableName);

                using (var command = new DB2Command(insertSql, connection))
                {
                    var insertCount = command.ExecuteNonQueryAsync();
                }

                using (var command = new DB2Command(countSql, connection))
                {
                    var hotelCount = command.ExecuteScalarAsync();
                }

                using (var command = new DB2Command(deleteSql, connection))
                {
                    var deleteCount = command.ExecuteNonQueryAsync();
                }
            }

            return string.Join(",", teamMembers);
        }

    }
}
