// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NewRelic.Agent.Api;
using NewRelic.Agent.Core.Database;
using NewRelic.Agent.Extensions.Providers.Wrapper;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CompositeTests.CrossAgentTests.SqlObfuscation
{
    [TestFixture]
    public class SqlObfuscationCrossAgentTests
    {
        private static CompositeTestAgent _compositeTestAgent;
        private IAgent _agent;
        private SqlObfuscator _obfuscator = SqlObfuscator.GetSqlObfuscator("obfuscated");
        private readonly List<string> validVendors = Enum.GetNames(typeof(DatastoreVendor)).Select(s => s.ToLower()).ToList();

        public static List<TestCaseData> SqlObfuscationTestDatas => GetSqlObfuscationTestDatas();

        [SetUp]
        public void Setup()
        {
            _compositeTestAgent = new CompositeTestAgent();
            _agent = _compositeTestAgent.GetAgent();
        }

        [TearDown]
        public static void TearDown()
        {
            _compositeTestAgent.Dispose();
        }

        [TestCaseSource(nameof(SqlObfuscationTestDatas))]
        public void SqlObfuscationCrossAgentTests_CrossAgentTests(SqlObfuscationTestData testData)
        {
            foreach (var dialect in testData.Dialects)
            {
                if (validVendors.Contains(dialect))
                {
                    var datastoreVendor = GetDatastoreVendorFromString(dialect);
                    var obfuscatedSql = _obfuscator.GetObfuscatedSql(testData.Sql, datastoreVendor);

                    var exists = testData.ObfuscatedSql.Contains(obfuscatedSql);
                    Console.WriteLine(obfuscatedSql);
                    Assert.That(exists, Is.True, "Failed for " + dialect.ToUpper());
                }
            }
        }

        private DatastoreVendor GetDatastoreVendorFromString(string dialect)
        {
            switch (dialect)
            {
                case "couchbase":
                    return DatastoreVendor.Couchbase;
                case "ibmdb2":
                    return DatastoreVendor.IBMDB2;
                case "memcached":
                    return DatastoreVendor.Memcached;
                case "mongodb":
                    return DatastoreVendor.MongoDB;
                case "mysql":
                    return DatastoreVendor.MySQL;
                case "mssql":
                    return DatastoreVendor.MSSQL;
                case "oracle":
                    return DatastoreVendor.Oracle;
                case "postgres":
                    return DatastoreVendor.Postgres;
                case "redis":
                    return DatastoreVendor.Redis;
                default:
                    return DatastoreVendor.Other;
            }
        }

        private static List<TestCaseData> GetSqlObfuscationTestDatas()
        {
            var testCaseDatas = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "SqlObfuscation", "sql_obfuscation.json");
            var jsonString = File.ReadAllText(jsonPath);

            var settings = new JsonSerializerSettings
            {
                Error = (sender, args) =>
                {
                    if (System.Diagnostics.Debugger.IsAttached)
                    {
                        System.Diagnostics.Debugger.Break();
                    }
                }
            };

            var testDatas = JsonConvert.DeserializeObject<List<SqlObfuscationTestData>>(jsonString, settings);

            foreach (var test in testDatas)
            {
                var testCase = new TestCaseData(test);
                testCase.SetName("SqlObfuscationCrossAgentTests " + test.Name);
                testCaseDatas.Add(testCase);
            }

            return testCaseDatas;
        }

        public class SqlObfuscationTestData
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("sql")]
            public string Sql { get; set; }

            [JsonProperty("obfuscated")]
            public List<string> ObfuscatedSql { get; set; }

            [JsonProperty("dialects")]
            public List<string> Dialects { get; set; }
        }
    }
}
