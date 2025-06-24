// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CompositeTests.HybridAgent.Helpers;
using NewRelic.Agent.TestUtilities;
using Newtonsoft.Json;
using NUnit.Framework;

namespace CompositeTests.CrossAgentTests.HybridAgent
{
    [TestFixture]
    public class HybridAgentCrossAgentTests : HybridAgentTestsBase
    {
        [TestCaseSource(nameof(GetHybridAgentSmokeTestData))]
        public override void Tests(HybridAgentTestCase test)
        {
            foreach (var operation in test.Operations ?? Enumerable.Empty<Operation>())
            {
                PerformOperation(operation);
            }

            TriggerOrWaitForHarvestCycle();

            ValidateTelemetryFromHarvest(test.Telemetry);
        }

        private static List<TestCaseData> GetHybridAgentSmokeTestData()
        {
            var testCaseDatas = new List<TestCaseData>();

            string location = Assembly.GetExecutingAssembly().GetLocation();
            var dllPath = Path.GetDirectoryName(new Uri(location).LocalPath);
            var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "HybridAgent", "TestCaseDefinitions", "HybridAgentSmokeTests.json");
            var jsonString = File.ReadAllText(jsonPath);
            var testList = JsonConvert.DeserializeObject<List<HybridAgentTestCase>>(jsonString);

            foreach (var testData in testList)
            {
                var testCase = new TestCaseData([testData]);
                testCase.SetName("HybridAgent_CrossAgent_SmokeTests: " + testData.TestDescription);
                testCaseDatas.Add(testCase);
            }

            return testCaseDatas;
        }
    }
}
