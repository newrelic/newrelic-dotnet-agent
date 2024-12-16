// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Globalization;
using System.Text.RegularExpressions;
#if NETFRAMEWORK
using System.Data.OleDb;
#endif
using NewRelic.Agent.Api;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Extensions.Parsing
{
    public static class SqlWrapperHelper
    {
        private const string NullQueryParameterValue = "Null";

        private static Regex _getDriverFromConnectionStringRegex = new Regex(@"DRIVER\=\{(.+?)\}");
        private static Dictionary<string, DatastoreVendor> _vendorNameCache = new Dictionary<string, DatastoreVendor>();

        /// <summary>
        /// Gets the name of the datastore being used by a dbCommand.
        /// </summary>
        /// <param name="command">The command to get the datastore name from</param>
        /// <param name="typeName">Optional. If included, this method will not spend any CPU cycles using reflection to determine the type name of command.</param>
        /// <returns></returns>
        public static DatastoreVendor GetVendorName(IDbCommand command)
        {

#if NETFRAMEWORK
            // If this is an OleDbCommand, the only way to give the data store name is by looking at the connection provider
            var oleCommand = command as OleDbCommand;
            if (oleCommand != null && oleCommand.Connection != null)
                return ExtractVendorNameFromString(oleCommand.Connection.Provider);

#endif
            return GetVendorName(command.GetType().Name);
        }

        public static DatastoreVendor GetVendorNameFromOdbcConnectionString(string connectionString)
        {
            // Example connection string: DRIVER={SQL Server Native Client 11.0};Server=127.0.0.1;Database=NewRelic;Trusted_Connection=no;UID=sa;PWD=MssqlPassw0rd;Encrypt=no;
            if (_vendorNameCache.TryGetValue(connectionString, out DatastoreVendor vendor))
            {
                return vendor;
            }

            var match = _getDriverFromConnectionStringRegex.Match(connectionString);
            if (match.Success)
            {
                var driver = match.Groups[1].Value;
                vendor = ExtractVendorNameFromString(driver);
                _vendorNameCache[connectionString] = vendor;
                return vendor;
            }
            return DatastoreVendor.ODBC; // or maybe Other?
        }

        public static DatastoreVendor GetVendorName(string typeName)
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
            { "DB2Command", DatastoreVendor.IBMDB2 }
        };

        /// <summary>
        /// Returns a consistently formatted vendor name from a connection string using known vendor name specifiers.
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        private static DatastoreVendor ExtractVendorNameFromString(string text)
        {
            if (text.IndexOf("SQL Server", StringComparison.OrdinalIgnoreCase) != -1 || text.IndexOf("SQLServer", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.MSSQL;

            if (text.IndexOf("MySql", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.MySQL;

            if (text.IndexOf("Oracle", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.Oracle;

            if (text.IndexOf("PgSql", StringComparison.OrdinalIgnoreCase) != -1 || text.IndexOf("Postgres", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.Postgres;

            if (text.IndexOf("DB2", StringComparison.OrdinalIgnoreCase) != -1 || text.IndexOf("IBM", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.IBMDB2;

            // This works for redshift since the driver reports as "Amazon Redshift ODBC Driver"
            // Other drivers may not report as expected
            if (text.IndexOf("ODBC", StringComparison.OrdinalIgnoreCase) != -1)
                return DatastoreVendor.ODBC;

            return DatastoreVendor.Other;
        }

        public static IDictionary<string, IConvertible> GetQueryParameters(IDbCommand command, IAgent agent)
        {
            if (!agent.Configuration.DatastoreTracerQueryParametersEnabled)
            {
                return null;
            }

            if (command.Parameters.Count == 0)
            {
                return null;
            }

            var result = new Dictionary<string, IConvertible>(command.Parameters.Count);

            foreach (var parameter in command.Parameters)
            {
                var dbDataParameter = parameter as IDbDataParameter;

                if (string.IsNullOrEmpty(dbDataParameter?.ParameterName))
                {
                    continue;
                }

                var value = GetValue(dbDataParameter);

                if (value != null)
                {
                    result.Add(dbDataParameter.ParameterName, value);
                }
            }

            return result;
        }

        private static IConvertible GetValue(IDbDataParameter dbDataParameter)
        {
            if (dbDataParameter.Value == null)
            {
                return NullQueryParameterValue;
            }

            switch (dbDataParameter.Value)
            {
                case Guid v:
                    return v.ToString();
                case char[] v:
                    return new string(v);
                case TimeSpan v:
                    return v.ToString("c");
                case DateTimeOffset v:
                    return v.ToString(CultureInfo.InvariantCulture);
                case DBNull _:
                    return NullQueryParameterValue;
                case IConvertible v:
                    return v;
                case SqlBoolean v:
                    return v.Value;
                case SqlByte v:
                    return v.Value;
                case SqlChars v:
                    return new string(v.Value);
                case SqlDateTime v:
                    return v.Value.ToString(CultureInfo.InvariantCulture);
                case SqlDecimal v:
                    return v.Value;
                case SqlDouble v:
                    return v.Value;
                case SqlGuid v:
                    return v.Value.ToString();
                case SqlInt16 v:
                    return v.Value;
                case SqlInt32 v:
                    return v.Value;
                case SqlInt64 v:
                    return v.Value;
                case SqlMoney v:
                    return v.Value;
                case SqlSingle v:
                    return v.Value;
                case SqlString v:
                    return v.Value;
                default:
                    return dbDataParameter.Value?.ToString();
            }
        }
    }
}
