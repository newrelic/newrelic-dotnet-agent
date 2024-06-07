// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NewRelic.Agent.Core.BrowserMonitoring;
using NUnit.Framework;

namespace NewRelic.Agent.Core.CrossAgentTests.RumTests
{
    //https://source.datanerd.us/newrelic/cross_agent_tests/tree/master/rum_loader_insertion_location
    [TestFixture, Category("BrowserMonitoring")]
    public class RumLoaderInsertionLocationTests
    {
        [Test]
        public void TestFilesCanEnumerate()
        {
            var testCases = GetRumTestData();
            Assert.That(testCases.Count(), Is.GreaterThan(0));
        }

        [Test, TestCaseSource(nameof(GetRumTestData))]
        public void cross_agent_browser_monitor_injection_using_BrowserMonitoringWriter(string fileName, string data, string expected)
        {
            var writer = new BrowserMonitoringWriter(() => "EXPECTED_RUM_LOADER_LOCATION");
            var result = writer.WriteScriptHeaders(data);
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test, TestCaseSource(nameof(GetRumTestData))]
        public async Task cross_agent_browser_monitor_injection_using_BrowserScriptInjectionHelper(string fileName, string data, string expected)
        {
            using (var ms = new MemoryStream())
            {
                var dataBytes = Encoding.UTF8.GetBytes(data);
                await BrowserScriptInjectionHelper.InjectBrowserScriptAsync(dataBytes, ms, Encoding.UTF8.GetBytes("EXPECTED_RUM_LOADER_LOCATION"), null);

                await ms.FlushAsync();
                ms.Position = 0;

                var resultBytes = ms.ToArray();
                var result = Encoding.UTF8.GetString(resultBytes);

                Assert.That(result, Is.EqualTo(expected));

            }
        }

        private static IEnumerable<TestCaseData> GetRumTestData()
        {
            var rumContentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"BrowserMonitoring\rum_loader_insertion_location\");

            return Directory.EnumerateFiles(rumContentDirectory, "*.html", SearchOption.TopDirectoryOnly)
                .Where(file => file != null)
                .Select(file =>
                {
                    var contents = File.ReadAllText(file);
                    return new TestCaseData(file, contents.Replace("EXPECTED_RUM_LOADER_LOCATION", ""), contents)
                        .SetName(new FileInfo(file).Name);
                });
        }
    }
}
