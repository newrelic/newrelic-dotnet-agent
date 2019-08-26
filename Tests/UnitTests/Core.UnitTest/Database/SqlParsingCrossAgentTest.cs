using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Parsing;
using Newtonsoft.Json;
using NUnit.Framework;


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
			var parsed = SqlParser.GetParsedDatabaseStatement(CommandType.Text, inputSql);

			Assert.AreEqual(expectedOperation.ToLower(), parsed.Operation, String.Format("Expected operation {0} but was {1}", expectedOperation, parsed.Operation));
			Assert.AreEqual(expectedTable?.ToLower(), parsed.Model, String.Format("Expected table {0} but was {1}", expectedTable, parsed.Model));
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
					var parsed = SqlParser.GetParsedDatabaseStatement(CommandType.Text, c.Arguments[0] as string);
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

			//sanitize the SQL statement so that it is a valid c# identifier to use as a test name (the test framework may fail otherwise)
			string SanitizeIdentifier(string input)
			{
				var sb = new StringBuilder(input);
				return sb.Replace(' ', '_').Replace("=", "_eq_").Replace("`", "_backtick_").Replace("'", "_apos_").Replace("\"", "_quote_").Replace("\r", "_carriagereturn_")
						.Replace("*", "_star_").Replace("(", "_leftparen_").Replace(")", "_rightparen_").Replace("[", "_leftbrk_").Replace("]", "_rightbrk_").Replace(">", "_gt_").Replace(",", "_comma_").Replace(".", "_dot_").Replace("?", "_questmark_")
						.Replace("\t", "_tab_").Replace("/", "_fslash_")
						.Replace("__", "_").Replace("__", "_").Replace("__", "_").ToString();
			}

			//valid .net identifier regex
			var rgx = new Regex(@"^[_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}][_\p{Lu}\p{Ll}\p{Lt}\p{Lm}\p{Lo}\p{Nl}\p{Mn}\p{Mc}\p{Nd}\p{Pc}\p{Cf}]*$");
			var testCaseData = testCases
				.Where(testCase => testCase != null && testCase.Input != null && testCase.ExpectedOperation != null)
				.Where(testCase => rgx.IsMatch(SanitizeIdentifier(testCase.Input)))
				.Select(testCase => new TestCaseData(testCase.Input, testCase.ExpectedOperation, testCase.ExpectedTable)
					.SetName("SqlParsingTest_" + SanitizeIdentifier(testCase.Input)));

			if (testCases.Count != testCaseData.Count())
			{
				throw new InvalidOperationException($"count of tests parsed from json ({testCases.Count}) does not match count of TestCases ({testCaseData.Count()})");
			}
			return testCaseData;
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
