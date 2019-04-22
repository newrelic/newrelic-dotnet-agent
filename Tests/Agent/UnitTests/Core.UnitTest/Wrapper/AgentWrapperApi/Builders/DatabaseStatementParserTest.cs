using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System.Data;
using System.Threading;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{

	[TestFixture]
	public class DatabaseStatementParserTest
	{
		private DatabaseStatementParser _databaseStatementParser;

		[SetUp]
		public void SetUp()
		{
			_databaseStatementParser = new DatabaseStatementParser();
		}

		[TearDown]
		public void TearDown()
		{
			_databaseStatementParser.Dispose();
		}

		[Test]
		public void ParseDatabaseStatement_SameStatementSameVendor_Matched()
		{
			_databaseStatementParser.CacheCapacity = 10;

			var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
			var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");

			//Use AreSame to ensure that we are getting a reference match.
			Assert.AreSame(statement, statement2);
		}

		[Test]
		public void ParseDatabaseStatement_DifferentStatementsSameVendor_NotMatched()
		{
			_databaseStatementParser.CacheCapacity = 10;

			var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
			var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from people");

			Assert.AreNotSame(statement, statement2);
		}

		[Test]
		public void ParseDatabaseStatement_SameStatementDifferentVendor_NotMatched()
		{
			_databaseStatementParser.CacheCapacity = 10;

			var statement = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
			var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.Oracle, CommandType.Text, "select * from users");

			Assert.AreNotSame(statement, statement2);
		}


		[Test]
		public void ParseDatabaseStatement_CommandTypeNotText_IsNotCached()
		{
			_databaseStatementParser.CacheCapacity = 10;

			var statement1 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "pHelloWorld");
			var statement2 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.StoredProcedure, "pHelloWorld");

			var statement3 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "users");
			var statement4 = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.TableDirect, "users");

			Assert.AreNotSame(statement1, statement2);
			Assert.AreNotSame(statement3, statement4);
		}

		[Test]
		public void CacheCapacity_ChangesApplied()
		{
			const string sql1 = "select * from table1";
			const string sql2 = "select * from table2";
			const string sql3 = "select * from table3";

			//Set initial capacity of cache to 2
			_databaseStatementParser.CacheCapacity = 2;

			_databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql1);
			_databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql2);
			var stmtA = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);

			Thread.Sleep(1000);//Allow cache to periodically clean

			var stmtB = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);

			//stmtA and stmtB are the same SQL, but stmtA was ejected from the cache because of the cache periodically cleanup, so they cannot be the same object reference
			//This tests our original capacity is being honored.
			Assert.AreNotSame(stmtA, stmtB);

			//Resize the cache
			_databaseStatementParser.CacheCapacity = 3;

			_databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql1);
			_databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql2);

			Thread.Sleep(1000);//Allow cache to periodically clean

			//stmtB and stmtC are the same SQL, but this time nothing was ejected because of the cache size is withing its capacity, so they are the same object reference
			var stmtC = _databaseStatementParser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, sql3);
			Assert.AreSame(stmtB, stmtC);
		}
	}
}
