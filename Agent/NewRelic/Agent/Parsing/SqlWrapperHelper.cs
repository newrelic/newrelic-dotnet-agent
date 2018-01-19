using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
#if NET45
using System.Data.Odbc;
using System.Data.OleDb;
#endif
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Parsing
{
	public static class SqlWrapperHelper
	{
		/// <summary>
		/// Gets the name of the datastore being used by a dbCommand.
		/// </summary>
		/// <param name="command">The command to get the datastore name from</param>
		/// <param name="typeName">Optional. If included, this method will not spend any CPU cycles using reflection to determine the type name of command.</param>
		/// <returns></returns>
		[NotNull, Pure]
		public static DatastoreVendor GetVendorName([NotNull] IDbCommand command)
		{

#if NET45
			// If this is an OdbcCommand, the only way to give the data store name is by looking at the connection driver

			var odbcCommand = command as OdbcCommand;
			if (odbcCommand != null && odbcCommand.Connection != null)
				return ExtractVendorNameFromString(odbcCommand.Connection.Driver);

			// If this is an OleDbCommand, the only way to give the data store name is by looking at the connection provider
			var oleCommand = command as OleDbCommand;
			if (oleCommand != null && oleCommand.Connection != null)
				return ExtractVendorNameFromString(oleCommand.Connection.Provider);
#endif
			return GetVendorName(command.GetType().Name);
		}

		public static DatastoreVendor GetVendorName([NotNull] String typeName)
		{

			if (Vendors.TryGetValue(typeName, out DatastoreVendor vendor))
			{
				return vendor;
			}

			return DatastoreVendor.Other;
		}

		private static readonly IDictionary<string, DatastoreVendor> Vendors = new Dictionary<string, DatastoreVendor>
		{
			{ "SqlCommand", DatastoreVendor.MSSQL },
			{ "MySqlCommand", DatastoreVendor.MySQL },
			{ "OracleCommand", DatastoreVendor.Oracle },
			{ "OracleDatabase", DatastoreVendor.Oracle },
			{ "NpgsqlCommand", DatastoreVendor.Postgres },
			{ "DB2Command", DatastoreVendor.IBMDB2 },
		};

		/// <summary>
		/// Returns a consistently formatted vendor name from a connection string using known vendor name specifiers.
		/// </summary>
		/// <param name="text"></param>
		/// <returns></returns>
		[NotNull, Pure]
		private static DatastoreVendor ExtractVendorNameFromString(String text)
		{
			text = text.ToLowerInvariant();
			if (text.Contains("SQL Server".ToLowerInvariant()) || text.Contains("SQLServer".ToLowerInvariant()))
				return DatastoreVendor.MSSQL;

			if (text.Contains("MySql".ToLowerInvariant()))
				return DatastoreVendor.MySQL;

			if (text.Contains("Oracle".ToLowerInvariant()))
				return DatastoreVendor.Oracle;

			if (text.Contains("PgSql".ToLowerInvariant()) || text.Contains("Postgres".ToLowerInvariant()))
				return DatastoreVendor.Postgres;

			if (text.Contains("DB2".ToLowerInvariant()) || text.Contains("IBM".ToLowerInvariant()))
				return DatastoreVendor.IBMDB2;

			return DatastoreVendor.Other;
		}
	}
}
