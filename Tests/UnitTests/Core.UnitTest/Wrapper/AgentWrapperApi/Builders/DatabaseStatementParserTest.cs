using NewRelic.Agent.Extensions.Providers.Wrapper;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewRelic.Agent.Core.Wrapper.AgentWrapperApi.Builders
{

	[TestFixture]
	public class DatabaseStatementParserTest
    {

		[Test]
		public void ParseDatabaseStatement_CacheWorks()
		{
			var parser = new DatabaseStatementParser();
			var statement = parser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
			var statement2 = parser.ParseDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, "select * from users");
			Assert.AreSame(statement, statement2);
		}
	}
}
