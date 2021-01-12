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

namespace ParsingTests
{
    [TestFixture]
    public class SqlParserTests
    {
        [Test]
        public void SqlParserTest_SelectQueryParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM MyAwesomeTable");
            Assert.AreEqual("myawesometable", parsedDatabaseStatement.Model);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
            Assert.AreEqual("Datastore/statement/MSSQL/myawesometable/select", parsedDatabaseStatement.DatastoreStatementMetricName);
        }

        [Test]
        public void SqlParserTest_StoredProcedureParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "dbo.MySchema.scalar_getMeSomeData");
            Assert.AreEqual("dbo.myschema.scalar_getmesomedata", parsedDatabaseStatement.Model);
            Assert.AreEqual("ExecuteProcedure", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_Valid_Sqls_But_NotSupported()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure'123'");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure\t'123'");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure\r\n'123'");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "[mystoredprocedure]123");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "\"mystoredprocedure\"abc");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);

            parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "mystoredprocedure");
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_TableDirectQueryParsed()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "MyAwesomeTable");
            Assert.AreEqual("MyAwesomeTable", parsedDatabaseStatement.Model);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_InvalidTextCantBeParsed_And_DoesNotResultInNullParsedDatabaseStatement()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Lorem ipsum dolar sit amet");
            Assert.IsNotNull(parsedDatabaseStatement); // It is important that GetParsedDatabaseStatement never returns null
            Assert.IsNull(parsedDatabaseStatement.Model);
            Assert.AreEqual("other", parsedDatabaseStatement.Operation);
            Assert.AreEqual(DatastoreVendor.MSSQL, parsedDatabaseStatement.DatastoreVendor);
        }

        [Test]
        public static void SqlParserTest_TestIsValidName()
        {
            Assert.IsTrue(SqlParser.IsValidName("dude"));
            Assert.IsTrue(SqlParser.IsValidName("Dude"));
            Assert.IsTrue(SqlParser.IsValidName("dude23"));
            Assert.IsTrue(SqlParser.IsValidName("$dude"));
            Assert.IsTrue(SqlParser.IsValidName("dude.man"));
            Assert.IsTrue(SqlParser.IsValidName("dude_man"));
            Assert.IsFalse(SqlParser.IsValidName(@"/dude"));
            Assert.IsFalse(SqlParser.IsValidName(@"dude\"));
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("id", parsedDatabaseStatement.Model);
            Assert.AreEqual("declare", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_TestWaitForStatement()
        {
            var parsedDatabaseStatementDelay = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "WaitFor Delay \"00:00:00.5\"");
            Assert.IsNotNull(parsedDatabaseStatementDelay);
            Assert.AreEqual("waitfor", parsedDatabaseStatementDelay.Operation);
            Assert.AreEqual("time", parsedDatabaseStatementDelay.Model);

            var parsedDatabaseStatementTime = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "WaitFor Time \"08:17:00\"");
            Assert.IsNotNull(parsedDatabaseStatementTime);
            Assert.AreEqual("waitfor", parsedDatabaseStatementTime.Operation);
            Assert.AreEqual("time", parsedDatabaseStatementTime.Model);
        }

        [Test]
        public void SqlParserTest_TestCompoundSetStatement()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set @FOO=17; set @BAR=18;");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("foo", parsedDatabaseStatement.Model);
            Assert.AreEqual("set", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_TestSelect_with_nocount()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set nocount on; select * from dude");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
            Assert.AreEqual("dude", parsedDatabaseStatement.Model);
        }

        [Test]
        public void SqlParserTest_TestCommentInFront()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"/* ignore the comment */
				select * from dude");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
            Assert.AreEqual("dude", parsedDatabaseStatement.Model);
        }

        [Test]
        public void SqlParserTest_TestCommentInMiddle()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, @"select *
				/* ignore the comment */
				from dude");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("dude/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestSelectWithBracket()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from [dude]");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("dude/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestSelectWithNestedParens()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from (((dude)))");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("dude/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestSelectMultipleLine()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Select *\nfrom MAN\nwhere id = 5");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("man/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestSelectMultipleTables()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SELECT * FROM man, dude where dude.id = man.id");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("man/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestUpdate()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "Update  dude set man = 'yeah' where id = 666");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("dude/update", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestSet()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SET character_set_results=NULL");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("character_set_results/set", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_set()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "SET nocount on ; select * from test");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_set_and_subselect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set nocount on;select * from test where this in (select * from testing)");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_beginning_comment()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "/* test */ select * from test");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_beginning_comment_and_set()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "/* test */ set nocount on; select * from test");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_verify_match_on_select_with_multi_sets()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "set test; set test2; select * from test");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestInsertWithSelect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "INSERT into   cars  select * from man");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("cars/insert", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestInsertWithValues()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "insert   into test(id, name) values(6, 'Bubba')");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("test/insert", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestDeleteFrom()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "delete from actors where title = 'The Dude'");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("actors/delete", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestDelete()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "delete actors where title = 'The Dude'");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("actors/delete", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestCreateTable()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "create table actors as select * from dudes");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("table/create", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestCreateProcedure()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "create procedure actors as select * from dudes");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("procedure/create", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestProcedure()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "MyProc");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("myproc/ExecuteProcedure", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestStoredProcedureTextCommand()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "sp_MyProc");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("sp_myproc/ExecuteProcedure", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestStoredProcedureTextCommandWithArguments()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "sp_MyProc ?, ?");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("sp_myproc/ExecuteProcedure", parsedDatabaseStatement.ToString());

        }

        [Test]
        public void SqlParserTest_TestProcedureWithBrackets()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "[DotNetNuke].[sys].[sp_dude]");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("dotnetnuke.sys.sp_dude/ExecuteProcedure", parsedDatabaseStatement.ToString());
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("classsearchpublicsite/ExecuteProcedure", parsedDatabaseStatement.ToString());
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("classsearchpublicsite/ExecuteProcedure", parsedDatabaseStatement.ToString());
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("classsearchpublicsite/ExecuteProcedure", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestExecProcedureNoArguments()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "EXEC @RTN = [ClassSearchPublicSite]");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("classsearchpublicsite/ExecuteProcedure", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestTableDirect()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "MyTable");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("MyTable/select", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestShow()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "show stuff");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("stuff/show", parsedDatabaseStatement.ToString());
        }

        [Test]
        public void SqlParserTest_TestShowLongName()
        {
            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "show wow_this_is_a_really_long_name_isnt_it_cmon_man_it_s_crazy_no_way_bruh");
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("show", parsedDatabaseStatement.Operation);
            Assert.AreEqual("wow_this_is_a_really_long_name_isnt_it_cmon_man_it", parsedDatabaseStatement.Model);
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("order", parsedDatabaseStatement.Model);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_TestSpecialCharsInTableName()
        {
            // Example of subbquery
            const string test = "DELETE FROM [ADI-?].[dbo].[UserSession] WHERE [SessionKey] = @p0";

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("usersession", parsedDatabaseStatement.Model);
            Assert.AreEqual("delete", parsedDatabaseStatement.Operation);
        }

        [Test]
        public void SqlParserTest_TestVariableSelect()
        {
            const string test = "SELECT x,y SELECT a,b if a > b";  // made up SQL

            var parsedDatabaseStatement = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, test);
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual("VARIABLE", parsedDatabaseStatement.Model);
            Assert.AreEqual("select", parsedDatabaseStatement.Operation);
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual(expectedModel, parsedDatabaseStatement.Model, $"Expected model {expectedModel} but was {parsedDatabaseStatement.Model}");
            Assert.AreEqual(expectedOperation, parsedDatabaseStatement.Operation, $"Expected operation {expectedOperation} but was {parsedDatabaseStatement.Operation}");
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
            Assert.IsNotNull(parsedDatabaseStatement);
            Assert.AreEqual(operation, parsedDatabaseStatement.Operation, $"Expected operation {operation} but was {parsedDatabaseStatement.Operation}");
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
                    Assert.IsNotNull(parsedDatabaseStatement);
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
            Assert.IsTrue(distinctParsers.Count() == 10);
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
    }
}
