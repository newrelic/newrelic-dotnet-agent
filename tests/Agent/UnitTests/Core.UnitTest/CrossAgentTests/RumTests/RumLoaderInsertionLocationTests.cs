﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
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
			Assert.Greater(testCases.Count(), 0);
		}

		[Test, TestCaseSource(nameof(GetRumTestData))]
		public void cross_agent_browser_monitor_injection_from_files(String fileName, String data, String expected)
		{
			var writer = new BrowserMonitoringWriter(() => "EXPECTED_RUM_LOADER_LOCATION");
			var result = writer.WriteScriptHeaders(data);
			Assert.AreEqual(expected, result);
		}

		[NotNull, UsedImplicitly]
		private static IEnumerable<TestCaseData> GetRumTestData()
		{
			var rumContentDirectory = Path.Combine(TestContext.CurrentContext.TestDirectory, @"BrowserMonitoring\rum_loader_insertion_location\");

			return Directory.EnumerateFiles(rumContentDirectory, "*.html", SearchOption.TopDirectoryOnly)
				.Where(file => file != null)
				.Select(file =>
				{
					var contents = File.ReadAllText(file);
					return new TestCaseData(file, contents.Replace("EXPECTED_RUM_LOADER_LOCATION", ""), contents).SetName(file);
				});
		}
	}
}
