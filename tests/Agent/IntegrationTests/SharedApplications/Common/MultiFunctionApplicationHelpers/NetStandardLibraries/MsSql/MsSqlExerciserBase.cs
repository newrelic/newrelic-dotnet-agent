// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

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
    }
}
