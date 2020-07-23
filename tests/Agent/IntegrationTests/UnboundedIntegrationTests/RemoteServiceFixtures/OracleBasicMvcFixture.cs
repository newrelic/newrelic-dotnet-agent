using System;
using System.Data.SqlClient;
using System.Net;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using NewRelic.Agent.IntegrationTests.Shared;
using Oracle.ManagedDataAccess.Client;
using Xunit;

namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
{
	public class OracleBasicMvcFixture : RemoteApplicationFixture
	{
		private const String CreateHotelTableOracleSql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
														 "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
		private const String DropHotelTableOracleSql = "DROP TABLE {0}";

		private readonly String _connectionString = $@"Data Source={OracleConfiguration.OracleServer}:{OracleConfiguration.OraclePort}/XE;User Id=SYSTEM;Password=!4maline!;";

		private readonly String _tableName;
		public String TableName
		{
			get { return _tableName; }
		}

		public OracleBasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
		{
			_tableName = GenerateTableName();
			CreateTable();
		}

		public void GetOracle()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/Oracle?tableName={TableName}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void GetOracleAsync()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/OracleAsync?tableName={TableName}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		public void GetEnterpriseLibraryOracle()
		{
			var address = $"http://{DestinationServerName}:{Port}/Default/EnterpriseLibraryOracle?tableName={TableName}";

			using (var webClient = new WebClient())
			{
				var responseBody = webClient.DownloadString(address);
				Assert.NotNull(responseBody);
			}
		}

		private static String GenerateTableName()
		{
			//Oracle tables must start w/ character and be <= 30 length. Table name = H{tableId}
			var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
			return $"h{tableId}";
		}

		private void CreateTable()
		{
			var createTable = String.Format(CreateHotelTableOracleSql, TableName);
			using (var connection = new OracleConnection(_connectionString))
			{
				connection.Open();

				using (var command = new OracleCommand(createTable, connection))
				{
					command.ExecuteNonQuery();
				}
			}
		}

		private void DropTable()
		{
			var dropTableSql = String.Format(DropHotelTableOracleSql, TableName);

			using (var connection = new OracleConnection(_connectionString))
			{
				connection.Open();

				using (var command = new OracleCommand(dropTableSql, connection))
				{
					command.ExecuteNonQuery();
				}
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			DropTable();
		}
	}
}
