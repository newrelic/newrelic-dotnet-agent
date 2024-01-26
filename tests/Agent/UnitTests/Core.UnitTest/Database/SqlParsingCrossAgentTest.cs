// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Parsing;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NewRelic.Agent.Core.NewRelic.Agent.Core.Database
{
    [TestFixture]
    public class SqlParsingCrossAgentTest
    {
        [TestCaseSource(nameof(GetSqlParsingTestCases))]
        public void SqlParsingTest(string inputSql, string expectedOperation, string expectedTable)
        {
            var parsed = SqlParser.GetParsedDatabaseStatement(DatastoreVendor.MSSQL, CommandType.Text, inputSql);
            Assert.Multiple(() =>
            {
                Assert.That(parsed.Operation, Is.EqualTo(expectedOperation.ToLower()), string.Format("Expected operation {0} but was {1}", expectedOperation, parsed.Operation));
                Assert.That(parsed.Model, Is.EqualTo(expectedTable?.ToLower()), string.Format("Expected table {0} but was {1}", expectedTable, parsed.Model));
            });
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
            public string Input;

            [JsonProperty(PropertyName = "operation")]
            public string ExpectedOperation;

            [JsonProperty(PropertyName = "table")]
            public string ExpectedTable;
        }
    }
}
