// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Data;
using Microsoft.Practices.EnterpriseLibrary.Data.Sql;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class MsSqlController : Controller
    {
        private const string InsertPersonMsSql = "INSERT INTO {0} (FirstName, LastName, Email) VALUES('Testy', 'McTesterson', 'testy@mctesterson.com')";
        private const string DeletePersonMsSql = "DELETE FROM {0} WHERE Email = 'testy@mctesterson.com'";
        private const string CountPersonMsSql = "SELECT COUNT(*) FROM {0} WITH(nolock)";

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
            _ = msSqlDatabase.ExecuteNonQuery(CommandType.Text, insertSql);
            _ = msSqlDatabase.ExecuteScalar(CommandType.Text, countSql);
            _ = msSqlDatabase.ExecuteNonQuery(CommandType.Text, deleteSql);

            return string.Join(",", teamMembers);
        }
    }
}
