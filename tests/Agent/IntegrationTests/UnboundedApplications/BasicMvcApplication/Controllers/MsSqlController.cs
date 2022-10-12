// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Data.OleDb;
using System.Data.SqlClient;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using System.Linq;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class MsSqlController : Controller
    {
        private const string InsertPersonMsSql = "INSERT INTO {0} (FirstName, LastName, Email) VALUES('Testy', 'McTesterson', 'testy@mctesterson.com')";
        private const string DeletePersonMsSql = "DELETE FROM {0} WHERE Email = 'testy@mctesterson.com'";
        private const string CountPersonMsSql = "SELECT COUNT(*) FROM {0} WITH(nolock)";
        private static readonly string CreateProcedureStatement = @"CREATE OR ALTER PROCEDURE [dbo].[{0}] {1} AS RETURN 0";

        [HttpGet]
        public string EnterpriseLibraryMsSql(string tableName)
        {
            var teamMembers = new List<string>();
            var msSqlDatabase = new SqlDatabase(MsSqlConfiguration.MsSqlConnectionString);

            using (var reader = msSqlDatabase.ExecuteReader(CommandType.Text, "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'John'"))
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
        public int MsSqlParameterizedStoredProcedureUsingOdbcDriver(string procedureName, bool paramsWithAtSign)
        {
            EnsureProcedure(procedureName, DbParameterData.OdbcMsSqlParameters);

            var parameterPlaceholder = string.Join(",", DbParameterData.OdbcMsSqlParameters.Select(_ => "?"));

            using (var connection = new OdbcConnection(MsSqlOdbcConfiguration.MsSqlOdbcConnectionString))
            using (var command = new OdbcCommand($"{{call {procedureName}({parameterPlaceholder})}}", connection))
            {
                connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.OdbcMsSqlParameters)
                {
                    var paramName = paramsWithAtSign
                        ? parameter.ParameterName
                        : parameter.ParameterName.TrimStart('@');

                    command.Parameters.Add(new OdbcParameter(paramName, parameter.Value)); ;
                }

                return command.ExecuteNonQuery();
            }
        }

        [HttpGet]
        public int MsSqlParameterizedStoredProcedureUsingOleDbDriver(string procedureName, bool paramsWithAtSign)
        {
            EnsureProcedure(procedureName, DbParameterData.OleDbMsSqlParameters);

            using (var connection = new OleDbConnection(MsSqlOleDbConfiguration.MsSqlOleDbConnectionString))
            using (var command = new OleDbCommand(procedureName, connection))
            {
                connection.Open();
                command.CommandType = CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.OleDbMsSqlParameters)
                {
                    var paramName = paramsWithAtSign
                        ? parameter.ParameterName
                        : parameter.ParameterName.TrimStart('@');

                    command.Parameters.Add(new OleDbParameter(paramName, parameter.Value));
                }

                return command.ExecuteNonQuery();
            }
        }

        private void EnsureProcedure(string procedureName, DbParameter[] dbParameters)
        {
            var parameters = string.Join(", ", dbParameters.Select(x => $"{x.ParameterName} {x.DbTypeName}"));
            var statement = string.Format(CreateProcedureStatement, procedureName, parameters);
            using (var connection = new SqlConnection(MsSqlConfiguration.MsSqlConnectionString))
            using (var command = new SqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}
