// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NET462

using System.Collections.Generic;
using System.Data;
using NewRelic.Agent.IntegrationTests.Shared;
using NewRelic.Agent.IntegrationTests.Shared.ReflectionHelpers;
using NewRelic.Api.Agent;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using sqlClient = System.Data.SqlClient;
using System.Threading;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql
{
    [Library]
    public class EnterpriseLibraryClientExerciser : MsSqlExerciserBase
    {
        private static string _connectionString = MsSqlConfiguration.MsSqlConnectionString;

        [LibraryMethod]
        [Transaction]
        public string MsSql(string tableName)
        {
            var teamMembers = new List<string>();
            var msSqlDatabase = new SqlDatabase(_connectionString);

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
            _ = msSqlDatabase.ExecuteNonQuery(CommandType.Text, insertSql);
            _ = msSqlDatabase.ExecuteScalar(CommandType.Text, countSql);
            _ = msSqlDatabase.ExecuteNonQuery(CommandType.Text, deleteSql);

            return string.Join(",", teamMembers);
        }

        [LibraryMethod]
        public void CreateTable(string tableName)
        {
            using (var connection = new sqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                var createTable = string.Format(CreatePersonTableMsSql, tableName);
                using (var command = new sqlClient.SqlCommand(createTable, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void DropTable(string tableName)
        {
            var dropTableSql = string.Format(DropPersonTableMsSql, tableName);

            using (var connection = new sqlClient.SqlConnection(_connectionString))
            {
                connection.Open();

                using (var command = new sqlClient.SqlCommand(dropTableSql, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        [LibraryMethod]
        public void Wait(int millisecondsTimeOut)
        {
            Thread.Sleep(millisecondsTimeOut);
        }
    }
}

#endif
