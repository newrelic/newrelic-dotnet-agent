// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CompositeTests.CrossAgentTests.SqlObfuscation;

/// <summary>
/// sql_obfuscation.json is a verbatim copy of
/// https://source.datanerd.us/newrelic/cross_agent_tests/blob/master/sql_obfuscation/sql_obfuscation.json (commit 3080dd984).
/// Never edit it by hand; record unsupported cases in KnownGaps instead.
/// </summary>
[TestFixture]
public class SqlObfuscationCrossAgentTests
{
    private static readonly SqlObfuscator Obfuscator = SqlObfuscator.GetSqlObfuscator("obfuscated");

    // Dialects in the suite that have no DatastoreVendor. The agent never applies their rules (these connections resolve to Other).
    private static readonly HashSet<string> DialectsWithoutVendor = new HashSet<string> { "sqlite", "cassandra" };

    // Cases the agent does not pass yet, as "<dialect>:<case name>". A listed case that starts passing fails the run, so the list cannot go stale.
    private static readonly HashSet<string> KnownGaps = new HashSet<string>
    {
        "mysql:back_quoted_identifiers.mysql",
        "mysql:end_of_query_comment_cstyle",
        "mysql:end_of_query_comment_doubledash",
        "mysql:end_of_query_comment_hash",
        "mysql:multiple_literal_types.mysql",
        "mysql:string_with_backslash_and_twin_single_quotes",
        "mysql:string_with_escaped_quotes.mysql",
        "mysql:string_with_trailing_backslash",
        "mysql:string_with_twin_single_quotes",
        "mysql:pathological/end_of_line_comments_with_quotes",
        "mysql:pathological/mixed_comments_and_quotes",
        "mysql:pathological/mixed_quotes_comments_and_newlines",
        "mysql:pathological/mixed_quotes_end_of_line_comments",
        "mysql:pathological/quote_delimiters_in_comments",
        "mysql:malformed/unterminated_double_quoted_string.mysql",
        "mysql:malformed/unterminated_single_quoted_string",
        "mysql:end_of_string_backslash_and_line_comment_with_quite",
        "mysql:hex_literals",
        "mysql:exponential_literals",
        "mysql:negative_integer_literals",
        "mysql:booleans",
        "mysql:prepended_comments_with_quotes.mysql",
        "mysql:prepended_multiline_comments_with_quotes.mysql",
        "oracle:double_quoted_identifiers.postgres",
        "oracle:end_of_query_comment_cstyle",
        "oracle:end_of_query_comment_doubledash",
        "oracle:end_of_query_comment_hash",
        "oracle:string_with_backslash_and_twin_single_quotes",
        "oracle:string_with_trailing_backslash",
        "oracle:string_with_twin_single_quotes",
        "oracle:pathological/end_of_line_comments_with_quotes",
        "oracle:pathological/mixed_comments_and_quotes",
        "oracle:pathological/mixed_quotes_comments_and_newlines",
        "oracle:pathological/mixed_quotes_end_of_line_comments",
        "oracle:pathological/quote_delimiters_in_comments",
        "oracle:malformed/unterminated_single_quoted_string",
        "oracle:end_of_string_backslash_and_line_comment_with_quite",
        "oracle:oracle_bracket_quote",
        "oracle:oracle_brace_quote",
        "oracle:oracle_angle_quote",
        "oracle:oracle_paren_quote",
        "oracle:exponential_literals",
        "oracle:negative_integer_literals",
    };

    public static IEnumerable<TestCaseData> TestCases => LoadTestCases();

    [TestCaseSource(nameof(TestCases))]
    public void SqlObfuscation(SqlObfuscationTestData testData, string dialect)
    {
        if (DialectsWithoutVendor.Contains(dialect))
        {
            Assert.Ignore($"The agent has no DatastoreVendor for '{dialect}'.");
        }

        var actual = Obfuscator.GetObfuscatedSql(testData.Sql, ToDatastoreVendor(dialect));

        if (KnownGaps.Contains($"{dialect}:{testData.Name}"))
        {
            Assert.That(testData.Obfuscated, Does.Not.Contain(actual), "This known gap now passes; remove it from KnownGaps.");
            Assert.Ignore("Known gap, not yet supported.");
        }

        Assert.That(testData.Obfuscated, Does.Contain(actual));
    }

    private static DatastoreVendor ToDatastoreVendor(string dialect)
    {
        switch (dialect)
        {
            case "mssql":
                return DatastoreVendor.MSSQL;
            case "mysql":
                return DatastoreVendor.MySQL;
            case "oracle":
                return DatastoreVendor.Oracle;
            case "postgres":
                return DatastoreVendor.Postgres;
            default:
                throw new ArgumentException($"Unmapped cross-agent dialect '{dialect}'.", nameof(dialect));
        }
    }

    private static IEnumerable<TestCaseData> LoadTestCases()
    {
        var location = Assembly.GetExecutingAssembly().GetLocation();
        var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
        var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "SqlObfuscation", "sql_obfuscation.json");
        var testDatas = JsonConvert.DeserializeObject<List<SqlObfuscationTestData>>(File.ReadAllText(jsonPath));

        // Periods and slashes in names make test explorers treat the name as a namespace.
        return testDatas.SelectMany(test => test.Dialects.Select(dialect =>
            new TestCaseData(test, dialect).SetName($"SqlObfuscation_{test.Name.Replace('.', '_').Replace('/', '_')}_{dialect}")));
    }

    public class SqlObfuscationTestData
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("sql")]
        public string Sql { get; set; }

        [JsonProperty("obfuscated")]
        public List<string> Obfuscated { get; set; }

        [JsonProperty("dialects")]
        public List<string> Dialects { get; set; }

        public override string ToString() => Name;
    }
}
