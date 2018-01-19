using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Parsing;
using Newtonsoft.Json;
using NUnit.Framework;
using NewRelic.Agent.Extensions.Providers.Wrapper;

namespace NewRelic.Agent.Core.NewRelic.Agent.Core.Database
{
	[TestFixture]
	public class SqlParsingCrossAgentTest
	{
		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), OneTimeSetUp]
		public void FixtureSetUp()
		{
			AgentBuilder.Build(false);
		}

		[TestCaseSource("GetSqlParsingTestCases")]
		public void SqlParsingTest([NotNull] String inputSql, [NotNull] String expectedOperation, [NotNull] String expectedTable)
		{
			var parsed = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, inputSql);
			Assert.AreEqual(expectedOperation.ToLower(), parsed.Operation, String.Format("Expected operation {0} but was {1}", expectedOperation, parsed.Operation));
			Assert.AreEqual(expectedTable.ToLower(), parsed.Model, String.Format("Expected table {0} but was {1}", expectedTable, parsed.Model));
		}


		// You can uncomment this to measure timing of sql parsing
		//[Test]
		public void Timing()
		{
			var stopwatch = Stopwatch.StartNew();

			var cases = GetSqlParsingTestCases();

			for (int i = 0; i < 1000000; i++)
			{
				foreach (var c in cases)
				{
					var parsed = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, c.Arguments[0] as string);
				}
			}

			stopwatch.Stop();

			Console.WriteLine("Time: " + stopwatch.ElapsedMilliseconds);

			Assert.Fail();
		}

		private static IEnumerable<TestCaseData> GetSqlParsingTestCases()
		{
			var testCases = JsonConvert.DeserializeObject<List<SqlParsingTestCase>>(SqlParsingCrossAgentTestJson.TestCases);
			if (testCases == null)
				throw new NullReferenceException("testCases");

			return testCases
				.Where(testCase => testCase != null)
				.Where(testCase => testCase.Input != null)
				.Where(testCase => testCase.ExpectedOperation != null)
				.Where(testCase => testCase.ExpectedTable != null)
				.Select(testCase =>
					new TestCaseData(testCase.Input, testCase.ExpectedOperation, testCase.ExpectedTable)
					.SetName(testCase.Input));
		}

		public class SqlParsingTestCase
		{
			[JsonProperty(PropertyName = "input")]
			public String Input;

			[JsonProperty(PropertyName = "operation")]
			public String ExpectedOperation;

			[JsonProperty(PropertyName = "table")]
			public string ExpectedTable;
		}
	}
}
