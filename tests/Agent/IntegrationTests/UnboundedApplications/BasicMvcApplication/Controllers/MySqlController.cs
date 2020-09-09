// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using MySql.Data.MySqlClient;
using NewRelic.Agent.IntegrationTests.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace BasicMvcApplication.Controllers
{
    public class MySqlController : Controller
    {
        [HttpGet]
        public string MySql()
        {
            var dates = new List<string>();

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 1", connection))
            {
                connection.Open();
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }

            return string.Join(",", dates);
        }

        [HttpGet]
        public async Task<string> MySqlAsync()
        {
            var dates = new List<string>();

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand("SELECT _date FROM dates WHERE _date LIKE '2%' ORDER BY _date DESC LIMIT 10000", connection))
            {
                connection.Open();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        dates.Add(reader.GetString(reader.GetOrdinal("_date")));
                    }
                }
            }

            return string.Join(",", dates);
        }

        [HttpGet]
        public int MySqlParameterizedStoredProcedure(string procedureName, bool paramsWithAtSigns)
        {
            CreateProcedure(procedureName);

            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(procedureName, connection))
            {
                connection.Open();
                command.CommandType = System.Data.CommandType.StoredProcedure;
                foreach (var parameter in DbParameterData.MySqlParameters)
                {
                    var sqlParam = paramsWithAtSigns
                        ? new MySqlParameter(parameter.ParameterName, parameter.Value)
                        : new MySqlParameter(parameter.ParameterName.TrimStart('@'), parameter.Value);

                    command.Parameters.Add(sqlParam);
                }

                return command.ExecuteNonQuery();
            }
        }


        private static readonly string CreateProcedureStatement = @"CREATE PROCEDURE `newrelic`.`{0}`({1}) BEGIN END;";

        private void CreateProcedure(string procedureName)
        {
            var parameters = string.Join(", ", DbParameterData.MySqlParameters.Select(x => $"{x.ParameterName} {x.DbTypeName}"));
            var statement = string.Format(CreateProcedureStatement, procedureName, parameters);
            using (var connection = new MySqlConnection(MySqlTestConfiguration.MySqlConnectionString))
            using (var command = new MySqlCommand(statement, connection))
            {
                connection.Open();
                command.ExecuteNonQuery();
            }
        }
    }
}
