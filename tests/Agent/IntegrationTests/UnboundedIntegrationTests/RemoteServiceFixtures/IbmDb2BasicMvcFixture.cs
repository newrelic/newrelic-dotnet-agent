// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

// Tests have a dependency on IBM software that is behind login-locked walls.
// using System;
// using System.Net;
// using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
// using Xunit;
// using IBM.Data.DB2;
// using NewRelic.Agent.IntegrationTests.Shared;

// namespace NewRelic.Agent.UnboundedIntegrationTests.RemoteServiceFixtures
// {
//     public class IbmDb2BasicMvcFixture : RemoteApplicationFixture
//     {
//         private const string CreateHotelTableDB2Sql = "CREATE TABLE {0} (HOTEL_ID INT NOT NULL, BOOKING_DATE DATE NOT NULL, " +
//                                                          "ROOMS_TAKEN INT DEFAULT 0, PRIMARY KEY (HOTEL_ID, BOOKING_DATE))";
//         private const string DropHotelTableDB2Sql = "DROP TABLE {0}";

//         public string TableName { get; }

//         public IbmDb2BasicMvcFixture() : base(new RemoteWebApplication("BasicMvcApplication", ApplicationType.Unbounded))
//         {
//             TableName = GenerateTableName();
//             CreateTable();
//         }

//         public void GetIbmDb2()
//         {
//             var address = $"http://{DestinationServerName}:{Port}/IbmDb2/InvokeIbmDb2Query?tableName={TableName}";

//             using (var webClient = new WebClient())
//             {
//                 var responseBody = webClient.DownloadString(address);
//                 Assert.NotNull(responseBody);
//             }
//         }

//         public void GetIbmDb2Async()
//         {
//             var address = $"http://{DestinationServerName}:{Port}/IbmDb2/InvokeIbmDb2QueryAsync?tableName={TableName}";

//             using (var webClient = new WebClient())
//             {
//                 var responseBody = webClient.DownloadString(address);
//                 Assert.NotNull(responseBody);
//             }
//         }

//         public void IbmDb2ParameterizedStoredProcedure(string procedureName)
//         {
//             var address = $"http://{DestinationServerName}:{Port}/IbmDb2/IbmDb2ParameterizedStoredProcedure?procedureName={procedureName}";

//             using (var webClient = new WebClient())
//             {
//                 var responseBody = webClient.DownloadString(address);
//                 Assert.NotNull(responseBody);
//             }
//         }

//         private static string GenerateTableName()
//         {
//             //Oracle tables must start w/ character and be <= 30 length. Table name = H{tableId}
//             var tableId = Guid.NewGuid().ToString("N").Substring(2, 29).ToLower();
//             return $"h{tableId}";
//         }

//         private void CreateTable()
//         {
//             var createTable = string.Format(CreateHotelTableDB2Sql, TableName);
//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             {
//                 connection.Open();

//                 using (var command = new DB2Command(createTable, connection))
//                 {
//                     command.ExecuteNonQuery();
//                 }
//             }
//         }

//         private void DropTable()
//         {
//             var dropTableSql = string.Format(DropHotelTableDB2Sql, TableName);

//             using (var connection = new DB2Connection(Db2Configuration.Db2ConnectionString))
//             {
//                 connection.Open();

//                 using (var command = new DB2Command(dropTableSql, connection))
//                 {
//                     command.ExecuteNonQuery();
//                 }
//             }
//         }

//         public override void Dispose()
//         {
//             base.Dispose();
//             DropTable();
//         }
//     }
// }
