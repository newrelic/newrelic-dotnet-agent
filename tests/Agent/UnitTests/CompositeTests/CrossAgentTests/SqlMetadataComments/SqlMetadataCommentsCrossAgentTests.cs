// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NewRelic.Agent.Extensions.Parsing;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CompositeTests.CrossAgentTests.SqlMetadataComments;

[TestFixture]
public class SqlMetadataCommentsCrossAgentTests
{
    public static List<TestCaseData> TestCases => GetTestCases();

    [TestCaseSource(nameof(TestCases))]
    public void SqlMetadataComments_CrossAgentTests(TestData testData)
    {
        var comment = SqlMetadataCommentBuilder.BuildComment(testData.EntityGuid);
        var result = SqlMetadataCommentBuilder.PrependCommentToSql(testData.OriginalSql, comment);

        Assert.That(result, Is.EqualTo(testData.ModifiedSql), testData.Description);
    }

    private static List<TestCaseData> GetTestCases()
    {
        var testCases = new List<TestCaseData>();

        var location = Assembly.GetExecutingAssembly().GetLocation();
        var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
        var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "SqlMetadataComments", "sql_metadata_comments.json");
        var jsonString = File.ReadAllText(jsonPath);

        var settings = new JsonSerializerSettings
        {
            Error = (sender, args) =>
            {
                if (System.Diagnostics.Debugger.IsAttached)
                    System.Diagnostics.Debugger.Break();
            }
        };

        var testDatas = JsonConvert.DeserializeObject<List<TestData>>(jsonString, settings);

        foreach (var test in testDatas)
        {
            var testCase = new TestCaseData(test);
            testCase.SetName("SqlMetadataCommentsCrossAgentTests " + test.Description);
            testCases.Add(testCase);
        }

        return testCases;
    }

    public class TestData
    {
        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("entity_guid")]
        public string EntityGuid { get; set; }

        [JsonProperty("original_sql")]
        public string OriginalSql { get; set; }

        [JsonProperty("modified_sql")]
        public string ModifiedSql { get; set; }
    }
}
