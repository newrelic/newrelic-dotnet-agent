// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Parsing;
using System.Data;
using System.Text;
using NUnit.Framework;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using System.Threading;
using System.Collections.Concurrent;
using NewRelic.Agent.Extensions.Parsing;
using System.Linq;
using System.Data.SqlClient;
using System.Collections.Generic;
using Microsoft.SqlServer.Server;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace ParsingTests
{
    [TestFixture]
    public class SqlParserTests
    {
        [Test]
        public void SqlParserTest_SelectQueryParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM MyAwesomeTable");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("myawesometable"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
                Assert.That(parsedDatabaseStatement.DatastoreStatementMetricName, Is.EqualTo("Datastore/statement/MSSQL/myawesometable/select"));
            });
        }

        [Test]
        public void SqlParserTest_StoredProcedureParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "dbo.MySchema.scalar_getMeSomeData");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("dbo.myschema.scalar_getmesomedata"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("ExecuteProcedure"));
            });
        }

        [Test]
        public void SqlParserTest_Valid_Sqls_But_NotSupported()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure'123'");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure\t'123'");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure\r\n'123'");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "[mystoredprocedure]123");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "\"mystoredprocedure\"abc");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
            });
        }

        [Test]
        public void SqlParserTest_TableDirectQueryParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "MyAwesomeTable");
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("MyAwesomeTable"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
            });
        }

        [Test]
        public void SqlParserTest_InvalidTextCantBeParsed_And_DoesNotResultInNullParsedDatabaseStatement()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Lorem ipsum dolar sit amet");
            Assert.That(parsedDatabaseStatement, Is.Not.Null); // It is important that GetParsedDatabaseStatement never returns null
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.Null);
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("other"));
                Assert.That(parsedDatabaseStatement.DatastoreVendor, Is.EqualTo(DatastoreVendor.MSSQL));
            });
        }

        [Test]
        public static void SqlParserTest_TestIsValidName()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SqlParser.IsValidName("dude"), Is.True);
                Assert.That(SqlParser.IsValidName("Dude"), Is.True);
                Assert.That(SqlParser.IsValidName("dude23"), Is.True);
                Assert.That(SqlParser.IsValidName("$dude"), Is.True);
                Assert.That(SqlParser.IsValidName("dude.man"), Is.True);
                Assert.That(SqlParser.IsValidName("dude_man"), Is.True);
                Assert.That(SqlParser.IsValidName(@"/dude"), Is.False);
                Assert.That(SqlParser.IsValidName(@"dude\"), Is.False);
            });
        }

        [Test]
        [TestCase("SELECT name FROM user", ExpectedResult = true)]
        [TestCase("SELECT name FROM user;", ExpectedResult = true)]
        [TestCase("SELECT name FROM user;   ", ExpectedResult = true)]
        [TestCase("SELECT name FROM user; DELETE FROM user", ExpectedResult = false)]
        // The test cases below this comment demonstrate the limitations of the IsSingleSqlCommand function - it has false positives on semicolons that are embedded in comments or in string literals
        [TestCase("SELECT name FROM user WHERE name like 'semi ; colon';", ExpectedResult = false)]
        [TestCase("/* This is just a comment but for some reason I put a semicolon in it; I hope this doesn't ruin anything */ SELECT name FROM user;", ExpectedResult = false)]

        public static bool SqlParserTest_TestIsSingleSqlStatement(string sql)
        {
            return SqlParser.IsSingleSqlStatement(sql);
        }


        [Test]
        public void SqlParserTest_TestDeclareStatement()
        {
            // Motivated by the petshop application.
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Declare @ID int");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("id"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("declare"));
            });
        }

        [Test]
        public void SqlParserTest_TestWaitForStatement()
        {
            var parsedDatabaseStatementDelay = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "WaitFor Delay \"00:00:00.5\"");
            Assert.That(parsedDatabaseStatementDelay, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatementDelay.Operation, Is.EqualTo("waitfor"));
                Assert.That(parsedDatabaseStatementDelay.Model, Is.EqualTo("time"));
            });

            var parsedDatabaseStatementTime = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "WaitFor Time \"08:17:00\"");
            Assert.That(parsedDatabaseStatementTime, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatementTime.Operation, Is.EqualTo("waitfor"));
                Assert.That(parsedDatabaseStatementTime.Model, Is.EqualTo("time"));
            });
        }

        [Test]
        public void SqlParserTest_TestCompoundSetStatement()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set @FOO=17; set @BAR=18;");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("foo"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("set"));
            });
        }

        [Test]
        public void SqlParserTest_TestSelect_with_nocount()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set nocount on; select * from dude");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("dude"));
            });
        }

        [Test]
        public void SqlParserTest_TestCommentInFront()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"/* ignore the comment */
				select * from dude");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("dude"));
            });
        }

        [Test]
        public void SqlParserTest_TestCommentInMiddle()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"select *
				/* ignore the comment */
				from dude");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude/select"));
        }

        [Test]
        public void SqlParserTest_PullNameFromComment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"select *
				/* NewRelicQueryName: DudeService.GetAllDudes */
				from dude");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("dude - [dudeservice.getalldudes]"));
                Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude - [dudeservice.getalldudes]/select"));
            });
        }
        
        [Test]
        public void SqlParserTest_PullNameFromComment_ReplaceSlashes()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"select *
				/* NewRelicQueryName: DudeService/GetAllDudes */
				from dude");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("dude - [dudeservice|getalldudes]"));

                // "dude - [dudeservice/getalldudes]/select" would cause "getalldudes" to be seen as the operation
                Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude - [dudeservice|getalldudes]/select"));
            });
        }
                

        [Test]
        public void SqlParserTest_TestSelectWithBracket()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from [dude]");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude/select"));
        }

        [Test]
        public void SqlParserTest_TestSelectWithNestedParens()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from (((dude)))");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude/select"));
        }

        [Test]
        public void SqlParserTest_TestSelectMultipleLine()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Select *\nfrom MAN\nwhere id = 5");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("man/select"));
        }

        [Test]
        public void SqlParserTest_TestSelectMultipleTables()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM man, dude where dude.id = man.id");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("man/select"));
        }

        [Test]
        public void SqlParserTest_TestUpdate()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Update  dude set man = 'yeah' where id = 666");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dude/update"));
        }

        [Test]
        public void SqlParserTest_TestSet()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SET character_set_results=NULL");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("character_set_results/set"));
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_set()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SET nocount on ; select * from test");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/select"));
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_set_and_subselect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set nocount on;select * from test where this in (select * from testing)");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/select"));
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_beginning_comment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "/* test */ select * from test");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/select"));
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_beginning_comment_and_set()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "/* test */ set nocount on; select * from test");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/select"));
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_multi_sets()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set test; set test2; select * from test");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/select"));
        }

        [Test]
        public void SqlParserTest_TestInsertWithSelect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "INSERT into   cars  select * from man");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("cars/insert"));
        }

        [Test]
        public void SqlParserTest_TestInsertWithValues()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "insert   into test(id, name) values(6, 'Bubba')");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("test/insert"));
        }

        [Test]
        public void SqlParserTest_TestDeleteFrom()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "delete from actors where title = 'The Dude'");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("actors/delete"));
        }

        [Test]
        public void SqlParserTest_TestDelete()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "delete actors where title = 'The Dude'");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("actors/delete"));
        }

        [Test]
        public void SqlParserTest_TestCreateTable()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "create table actors as select * from dudes");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("table/create"));
        }

        [Test]
        public void SqlParserTest_TestCreateProcedure()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "create procedure actors as select * from dudes");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("procedure/create"));
        }

        [Test]
        public void SqlParserTest_TestProcedure()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "MyProc");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("myproc/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestStoredProcedureTextCommand()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "sp_MyProc");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("sp_myproc/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestStoredProcedureTextCommandWithArguments()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "sp_MyProc ?, ?");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("sp_myproc/ExecuteProcedure"));

        }

        [Test]
        public void SqlParserTest_TestProcedureWithBrackets()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "[DotNetNuke].[sys].[sp_dude]");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("dotnetnuke.sys.sp_dude/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestExecProcedureWithReturnAssignment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "EXEC @RETURN_VALUE = [ClassSearchPublicSite] @programArea_ID = @p?,"
                                            + " @courseTitle = @p?,"
                                            + " @eventID = @p?,"
                                            + " @courseType = @p?,"
                                            + " @location = @p?,"
                                            + " @startDate = @p?,"
                                            + " @endDate = @p?,"
                                            + " @geo_Area = @p?,"
                                            + " @excludeInternational = @p?");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("classsearchpublicsite/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestExecProcedureNoAssignment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "EXEC [ClassSearchPublicSite] @programArea_ID = @p?,"
                                            + " @courseTitle = @p?,"
                                            + " @eventID = @p?,"
                                            + " @courseType = @p?,"
                                            + " @location = @p?,"
                                            + " @startDate = @p?,"
                                            + " @endDate = @p?,"
                                            + " @geo_Area = @p?,"
                                            + " @excludeInternational = @p?");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("classsearchpublicsite/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestExecuteProcedureNoAssignment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "EXECUTE [ClassSearchPublicSite] @programArea_ID = @p?,"
                                            + " @courseTitle = @p?,"
                                            + " @eventID = @p?,"
                                            + " @courseType = @p?,"
                                            + " @location = @p?,"
                                            + " @startDate = @p?,"
                                            + " @endDate = @p?,"
                                            + " @geo_Area = @p?,"
                                            + " @excludeInternational = @p?");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("classsearchpublicsite/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestExecProcedureNoArguments()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "EXEC @RTN = [ClassSearchPublicSite]");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("classsearchpublicsite/ExecuteProcedure"));
        }

        [Test]
        public void SqlParserTest_TestTableDirect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "MyTable");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("MyTable/select"));
        }

        [Test]
        public void SqlParserTest_TestShow()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "show stuff");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.ToString(), Is.EqualTo("stuff/show"));
        }

        [Test]
        public void SqlParserTest_TestShowLongName()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "show wow_this_is_a_really_long_name_isnt_it_cmon_man_it_s_crazy_no_way_bruh");
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("show"));
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("wow_this_is_a_really_long_name_isnt_it_cmon_man_it"));
            });
        }

        [Test]
        public void SqlParserTest_TestSpaceInFieldNames()
        {
            // Example of subbquery
            const string test = "SELECT Ord.OrderID, Ord.OrderDate," +
                       " (SELECT MAX(OrdDet.UnitPrice)" +
                        " FROM Northwind.dbo.[Order Details] AS OrdDet" +
                        " WHERE Ord.OrderID = OrdDet.OrderID) AS MaxUnitPrice" +
                        " FROM Northwind.dbo.Orders AS Ord";

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("order"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
            });
        }

        [Test]
        public void SqlParserTest_TestSpecialCharsInTableName()
        {
            // Example of subbquery
            const string test = "DELETE FROM [ADI-?].[dbo].[UserSession] WHERE [SessionKey] = @p0";

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("usersession"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("delete"));
            });
        }

        [Test]
        public void SqlParserTest_TestVariableSelect()
        {
            const string test = "SELECT x,y SELECT a,b if a > b";  // made up SQL

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo("VARIABLE"));
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo("select"));
            });
        }

        [Test]
        public void SqlParserTest_TestInnerSelect()
        {
            // Examine the statement that parses SELECT, and note that it will not match if the stuff
            // after the FROM contains a parentheses.  Here's an example that works, from Marina and now part of the test
            // DotNetTestApp/test.sqlserver.aspx
            const string test = "SELECT * FROM (SELECT * FROM [dbo].[Account] Where UserId like 'John') as test";
            const string expectedModel = "(subquery)";
            const string expectedOperation = "select";

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(parsedDatabaseStatement.Model, Is.EqualTo(expectedModel), $"Expected model {expectedModel} but was {parsedDatabaseStatement.Model}");
                Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo(expectedOperation), $"Expected operation {expectedOperation} but was {parsedDatabaseStatement.Operation}");
            });
        }

        [Test]
        [TestCase("SELECT * FROM people", "select")]
        [TestCase("INSERT INTO people(firstname, lastname) values ('alice', 'smith');", "insert")]
        [TestCase("UPDATE dude SET man = 'yeah' WHERE id = 123", "update")]
        [TestCase("DELETE FROM actors WHERE title = 'The Dude'", "delete")]
        [TestCase("EXEC dbo.spSomeProcOnOurSystem", "ExecuteProcedure")]
        public void SqlParserTest_IgnoreSetStatements(string commandText, string operation)
        {
            // We want to prioritize the 'real' command over the leading SET statements
            var test = "SET CONTEXT_INFO = @0; SET NOCOUNT ON; " + commandText;

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.That(parsedDatabaseStatement, Is.Not.Null);
            Assert.That(parsedDatabaseStatement.Operation, Is.EqualTo(operation), $"Expected operation {operation} but was {parsedDatabaseStatement.Operation}");
        }

        /// <summary>
        /// Make sure the parser can parse a number of different SQL statements with random alpha-numeric strings in them.
        /// </summary>
        [Test]
        public void SqlParserTest_TestFromGeneratedGibberish()
        {
            int[] lengths = { 5, 50, 500 };
            foreach (int length in lengths)
            {
                string[] prefixes = {
                    "sp_%",
                    "insert into %",
                    "update %",
                    "delete from %",
                    "select % from %",
                    "create %",
                    "drop %",
                    "set %",
                    "declare %",
                    "waitfor delay %",
                    "waitfor time %"
                };
                foreach (string s in prefixes)
                {
                    StringBuilder sb = new StringBuilder();
                    string[] splits = s.Split(new char[] { '%' });
                    for (int i = 0; i < splits.Length; i++)
                    {
                        string substring = splits[i];
                        if (substring.Length > 0)
                        {
                            sb.Append(substring);
                            if (i < splits.Length - 1)
                            {
                                sb.Append(RandomString(length));
                            }
                        }
                    }
                    string test = sb.ToString();

                    var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
                    Assert.That(parsedDatabaseStatement, Is.Not.Null);
                }
            }
        }


        [Test]
        public void SqlParserTest_NullSqlParserCache_IsThreadSafe()
        {
            var results = new ConcurrentBag<ParsedSqlStatement>();

            void work()
            {
                for (var c = 0; c < 1000; c++)
                {
                    DatastoreVendor vendor;
                    switch (c % 10)
                    {
                        case 0:
                            vendor = DatastoreVendor.Couchbase;
                            break;
                        case 1:
                            vendor = DatastoreVendor.IBMDB2;
                            break;
                        case 2:
                            vendor = DatastoreVendor.Memcached;
                            break;
                        case 3:
                            vendor = DatastoreVendor.MongoDB;
                            break;
                        case 4:
                            vendor = DatastoreVendor.MSSQL;
                            break;
                        case 5:
                            vendor = DatastoreVendor.MySQL;
                            break;
                        case 6:
                            vendor = DatastoreVendor.Oracle;
                            break;
                        case 7:
                            vendor = DatastoreVendor.Other;
                            break;
                        case 8:
                            vendor = DatastoreVendor.Postgres;
                            break;
                        default:
                            vendor = DatastoreVendor.Redis;
                            break;
                    }

                    var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(vendor, CommandType.Text, "Lorem ipsum dolar sit amet");
                    results.Add(parsedDatabaseStatement);
                }
            }

            var threads = new Thread[50];

            ThreadStart threadStart = new ThreadStart(work);

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(threadStart);
            }

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i].Start();
            }

            for (var i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            var distinctParsers = results.Distinct().ToList();
            Assert.That(distinctParsers.Count(), Is.EqualTo(10));
        }

    [Test]
        [TestCase("SELECT * FROM faketable WHERE [name] = @onlyParam",
            "SELECT * FROM faketable WHERE [name] = 'onlyValue'",
            new string[1] { "@onlyParam=onlyValue" }
            )]
        [TestCase("SELECT * FROM faketable WHERE [name] = @firstParam AND [quantity] = @secondParam",
            "SELECT * FROM faketable WHERE [name] = 'firstValue' AND [quantity] = 'secondValue'",
            new string[2] { "@firstParam=firstValue", "@secondParam=secondValue" }
            )]
        [TestCase("SELECT * FROM faketable WHERE [name] = @matchingPrefix AND [quantity] = @matchingPrefixPlus",
            "SELECT * FROM faketable WHERE [name] = 'firstValue' AND [quantity] = 'secondValue'",
            new string[2] { "@matchingPrefix=firstValue", "@matchingPrefixPlus=secondValue" }
            )]
        [TestCase("SELECT * FROM faketable WHERE [name] = @matchingPrefixPlus AND [quantity] = @matchingPrefix",
            "SELECT * FROM faketable WHERE [name] = 'firstValue' AND [quantity] = 'secondValue'",
            new string[2] { "@matchingPrefixPlus=firstValue", "@matchingPrefix=secondValue" }
            )]
        [TestCase("SELECT * FROM faketable",
            "SELECT * FROM faketable",
            new string[0] { }
            )]
        public void SqlParserTest_FixParameterizedSql_CorrectlyParsesParameters_String(string originalSql, string expectedSql, string[] sqlParameters)
        {
            // prepare
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(originalSql, emptyConnection);

            if (sqlParameters.Length > 0)
            {
                foreach (var sqlParameter in sqlParameters)
                {
                    var splitParam = sqlParameter.Split('=');
                    sqlCommand.Parameters.AddWithValue(splitParam[0], DbType.String); // this is more specific than allowing the class to infer the type.
                    sqlCommand.Parameters[splitParam[0]].Value = splitParam[1];
                }
            }

            // execute
            var shouldGeneratePlan = SqlParser.FixParameterizedSql(sqlCommand);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(expectedSql, Is.EqualTo(sqlCommand.CommandText), $"Expected '{expectedSql}', but got '{sqlCommand.CommandText}'");
                Assert.That(shouldGeneratePlan, Is.True, "FixParameterizedSql should return true if it is parsing a statement.");
            });
        }

        [TestCase("SELECT * FROM faketable WHERE [name] = @onlyParam",
            "SELECT * FROM faketable WHERE [name] = 1",
            "@onlyParam",
            true
            )]
        [TestCase("SELECT * FROM faketable WHERE [name] = @onlyParam",
            "SELECT * FROM faketable WHERE [name] = 0",
            "@onlyParam",
            false
            )]
        public void SqlParserTest_FixParameterizedSql_CorrectlyParsesParameters_Boolean(string originalSql, string expectedSql, string sqlParameterName, bool sqlParameterValue)
        {
            // prepare
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(originalSql, emptyConnection);

            sqlCommand.Parameters.Add(sqlParameterName, SqlDbType.Bit); // this is more specific than allowing the class to infer the type.
            sqlCommand.Parameters[sqlParameterName].Value = sqlParameterValue;

            // execute
            var shouldGeneratePlan = SqlParser.FixParameterizedSql(sqlCommand);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(expectedSql, Is.EqualTo(sqlCommand.CommandText), $"Expected '{expectedSql}', but got '{sqlCommand.CommandText}'");
                Assert.That(shouldGeneratePlan, Is.True, "FixParameterizedSql should return true if it is parsing a statement.");
            });
        }

        [TestCase("SELECT * FROM faketable WHERE [name] = @onlyParam",
            "SELECT * FROM faketable WHERE [name] = 'stuff'",
            "@onlyParam",
            "stuff"
            )]
        public void SqlParserTest_FixParameterizedSql_CorrectlyParsesParameters_Object_As_RealType(string originalSql, string expectedSql, string sqlParameterName, object sqlParameterValue)
        {
            // prepare
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(originalSql, emptyConnection);

            sqlCommand.Parameters.AddWithValue(sqlParameterName, sqlParameterValue);

            // execute
            var shouldGeneratePlan = SqlParser.FixParameterizedSql(sqlCommand);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(expectedSql, Is.EqualTo(sqlCommand.CommandText), $"Expected '{expectedSql}', but got '{sqlCommand.CommandText}'");
                Assert.That(shouldGeneratePlan, Is.True, "FixParameterizedSql should return true if it is parsing a statement.");
            });
        }

#if NETFRAMEWORK
        [TestCaseSource(nameof(BinaryTestDatas))]
        public void SqlParserTest_FixParameterizedSql_DoesNotParse_Binary(string originalSql, string expectedSql, string sqlParameterName, object sqlParameterValue)
        {
            // prepare
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(originalSql, emptyConnection);

            sqlCommand.Parameters.Add(sqlParameterName, SqlDbType.Binary); // this is more specific than allowing the class to infer the type.
            sqlCommand.Parameters[sqlParameterName].Value = sqlParameterValue;

            // execute
            var shouldGeneratePlan = SqlParser.FixParameterizedSql(sqlCommand);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(expectedSql, Is.EqualTo(sqlCommand.CommandText), $"Expected '{expectedSql}', but got '{sqlCommand.CommandText}'");
                Assert.That(shouldGeneratePlan, Is.False, "FixParameterizedSql should return false if it is not parsing a statement");
            });
        }
#endif

        [TestCaseSource(nameof(CustomObjectTestDatas))]
        public void SqlParserTest_FixParameterizedSql_DoesNotParse_CustomObject(string originalSql, string expectedSql, string sqlParameterName, object sqlParameterValue)
        {
            // prepare
            var emptyConnection = new SqlConnection("Server=falsehost;Database=fakedb;User Id=afakeuser;Password=notarealpasword;"); // not used for anything
            var sqlCommand = new SqlCommand(originalSql, emptyConnection);

            sqlCommand.Parameters.Add(sqlParameterName, SqlDbType.Structured); // translates to DbType.Object but allows more object types
            sqlCommand.Parameters[sqlParameterName].Value = sqlParameterValue;

            // execute
            var shouldGeneratePlan = SqlParser.FixParameterizedSql(sqlCommand);

            Assert.Multiple(() =>
            {
                // assert
                Assert.That(expectedSql, Is.EqualTo(sqlCommand.CommandText), $"Expected '{expectedSql}', but got '{sqlCommand.CommandText}'");
                Assert.That(shouldGeneratePlan, Is.False, "FixParameterizedSql should return false if it is not parsing a statement");
            });
        }

#if NETFRAMEWORK
        public static IEnumerable<TestCaseData> BinaryTestDatas
        {
            get
            {
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    System.Text.Encoding.UTF8.GetBytes("stuff"));
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    BitConverter.GetBytes(42));
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    ObjectToByteArray(new List<bool> { true, false }));
            }
        }
#endif

        public static IEnumerable<TestCaseData> CustomObjectTestDatas
        {
            get
            {
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    new SqlDataRecord());
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    "string");
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "SELECT * FROM faketable WHERE [name] = @onlyParam",
                    "@onlyParam",
                    DBNull.Value);
                yield return new TestCaseData("SELECT * FROM faketable WHERE [name] = @onlyParam",
                   "SELECT * FROM faketable WHERE [name] = @onlyParam",
                   "@onlyParam",
                   new List<SqlDataRecord>());

            }
        }

        private readonly Random _rng = new Random();
        private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        private string RandomString(int size)
        {
            char[] buffer = new char[size];
            for (int i = 0; i < size; i++)
            {
                buffer[i] = _chars[_rng.Next(_chars.Length)];
            }
            return new string(buffer);
        }
#if NETFRAMEWORK
        private static byte[] ObjectToByteArray(object obj)
        {
            using (var ms = new MemoryStream())
            {
                var bf = new BinaryFormatter();
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }
#endif
    }
}
