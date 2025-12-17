// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Text;

namespace MultiFunctionApplicationHelpers.NetStandardLibraries.MsSql
{
    public class MsSqlExerciserBase
    {

        protected const string SelectPersonByFirstNameMsSql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = 'John'";
        protected const string SelectPersonByLastNameMsSql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE LastName = 'Doe'";
        protected const string SelectPersonByParameterizedFirstNameMsSql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE FirstName = @FN";
        protected const string SelectPersonByParameterizedLastNameMsSql = "SELECT * FROM NewRelic.dbo.TeamMembers WHERE LastName = @LN";
        protected const string InsertPersonMsSql = "INSERT INTO {0} (FirstName, LastName, Email) VALUES('Testy', 'McTesterson', 'testy@mctesterson.com')";
        protected const string DeletePersonMsSql = "DELETE FROM {0} WHERE Email = 'testy@mctesterson.com'";
        protected const string CountPersonMsSql = "SELECT COUNT(*) FROM {0} WITH(nolock)";
        protected static readonly string CreateProcedureStatement = @"CREATE OR ALTER PROCEDURE [dbo].[{0}] {1} AS RETURN 0";
        protected const string CreatePersonTableMsSql = "CREATE TABLE {0} (FirstName varchar(20) NOT NULL, LastName varchar(20) NOT NULL, Email varchar(50) NOT NULL)";
        protected const string DropPersonTableMsSql = "IF (OBJECT_ID('{0}') IS NOT NULL) DROP TABLE {0}";
        protected const string DropProcedureSql = "IF (OBJECT_ID('{0}') IS NOT NULL) DROP PROCEDURE {0}";

        // Generate a SQL query longer than 4096 bytes to test truncation
        // The query is designed to be valid SQL and exceed the 4096 byte limit
        public static string GenerateLongSqlQuery()
        {
            var sb = new StringBuilder();
            // Start with a SELECT statement
            sb.Append("SELECT FirstName, LastName, Email FROM NewRelic.dbo.TeamMembers WHERE 1=1");
            
            // Add many OR conditions to exceed the 4096 byte limit
            // Each condition adds approximately 30-40 bytes
            // We need about 4100 bytes, so add enough conditions to reach that
            for (int i = 0; i < 150; i++)
            {
                sb.Append($" OR (FirstName = 'TestFirstName{i}' AND LastName = 'TestLastName{i}')");
            }
            
            return sb.ToString();
        }
    }
}
