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
    private static readonly HashSet<string> KnownGaps = new HashSet<string>();

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
