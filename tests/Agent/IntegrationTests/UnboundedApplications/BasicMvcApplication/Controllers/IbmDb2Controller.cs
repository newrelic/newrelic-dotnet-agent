// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Tests have a dependency on IBM software that is behind login-locked walls.
// using IBM.Data.DB2;
// using NewRelic.Agent.IntegrationTests.Shared;
// using System.Collections.Generic;
// using System.Data;
// using System.Linq;
// using System.Threading.Tasks;
// using System.Web.Mvc;

// namespace BasicMvcApplication.Controllers
// {
//     public class IbmDb2Controller : Controller
//     {
//         private const string InsertHotelDB2Sql = "INSERT INTO {0} (HOTEL_ID, BOOKING_DATE) VALUES (1, SYSDATE)";
//         private const string DeleteHotelDB2Sql = "DELETE FROM {0} WHERE HOTEL_ID = 1";
//         private const string CountHotelDB2Sql = "SELECT COUNT(*) FROM {0}";

//         [HttpGet]
//         public string InvokeIbmDb2Query(string tableName)
//         {
//             var teamMembers = new List<string>();

//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             {
//                 connection.Open();

//                 using (var command = new DB2Command("SELECT LASTNAME FROM EMPLOYEE FETCH FIRST ROW ONLY", connection))
//                 {
//                     using (var reader = command.ExecuteReader())
//                     {
//                         while (reader.Read())
//                         {
//                             teamMembers.Add(reader.GetString(reader.GetOrdinal("LASTNAME")));
//                         }
//                     }
//                 }

//                 var insertSql = string.Format(InsertHotelDB2Sql, tableName);
//                 var countSql = string.Format(CountHotelDB2Sql, tableName);
//                 var deleteSql = string.Format(DeleteHotelDB2Sql, tableName);

//                 using (var command = new DB2Command(insertSql, connection))
//                 {
//                     var insertCount = command.ExecuteNonQuery();
//                 }

//                 using (var command = new DB2Command(countSql, connection))
//                 {
//                     var hotelCount = command.ExecuteScalar();
//                 }

//                 using (var command = new DB2Command(deleteSql, connection))
//                 {
//                     var deleteCount = command.ExecuteNonQuery();
//                 }
//             }

//             return string.Join(",", teamMembers);
//         }

//         [HttpGet]
//         public async Task<string> InvokeIbmDb2QueryAsync(string tableName)
//         {
//             var teamMembers = new List<string>();

//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             {
//                 connection.Open();

//                 using (var command = new DB2Command("SELECT LASTNAME FROM EMPLOYEE FETCH FIRST ROW ONLY", connection))
//                 {
//                     using (var reader = await command.ExecuteReaderAsync())
//                     {
//                         while (await reader.ReadAsync())
//                         {
//                             teamMembers.Add(reader.GetString(reader.GetOrdinal("LASTNAME")));
//                         }
//                     }
//                 }

//                 var insertSql = string.Format(InsertHotelDB2Sql, tableName);
//                 var countSql = string.Format(CountHotelDB2Sql, tableName);
//                 var deleteSql = string.Format(DeleteHotelDB2Sql, tableName);

//                 using (var command = new DB2Command(insertSql, connection))
//                 {
//                     var insertCount = command.ExecuteNonQueryAsync();
//                 }

//                 using (var command = new DB2Command(countSql, connection))
//                 {
//                     var hotelCount = command.ExecuteScalarAsync();
//                 }

//                 using (var command = new DB2Command(deleteSql, connection))
//                 {
//                     var deleteCount = command.ExecuteNonQueryAsync();
//                 }
//             }

//             return string.Join(",", teamMembers);
//         }

//         [HttpGet]
//         public void IbmDb2ParameterizedStoredProcedure(string procedureName)
//         {
//             CreateProcedure(procedureName);

//             try
//             {
//                 using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//                 using (var command = new DB2Command(procedureName, connection))
//                 {
//                     connection.Open();
//                     command.CommandType = CommandType.StoredProcedure;

//                     foreach (var p in DbParameterData.IbmDb2Parameters)
//                     {
//                         command.Parameters.Add(p.ParameterName, p.Value);
//                     }

//                     command.ExecuteNonQuery();
//                 }
//             }
//             finally
//             {
//                 DropProcedure(procedureName);
//             }
//         }

//         private readonly string createProcedureStatment = @"CREATE PROCEDURE {0} ({1}) BEGIN END";
//         private readonly string dropProcedureStatement = @"DROP PROCEDURE {0}";

//         private void CreateProcedure(string procedureName)
//         {
//             var parameters = string.Join(", ", DbParameterData.IbmDb2Parameters.Select(x => $"IN {x.ParameterName} {x.DbTypeName}"));
//             var statement = string.Format(createProcedureStatment, procedureName, parameters);
//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             using (var command = new DB2Command(statement, connection))
//             {
//                 connection.Open();
//                 command.ExecuteNonQuery();
//             }
//         }

//         private void DropProcedure(string procedureName)
//         {
//             var statement = string.Format(dropProcedureStatement, procedureName);
//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             using (var command = new DB2Command(statement, connection))
//             {
//                 connection.Open();
//                 command.ExecuteNonQuery();
//             }
//         }
//     }
// }
