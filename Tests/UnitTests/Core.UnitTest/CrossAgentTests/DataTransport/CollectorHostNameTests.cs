using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Config;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using Telerik.JustMock;
using Newtonsoft.Json.Linq;
using NewRelic.Agent.Core.Configuration;
using NewRelic.Agent.Core.DataTransport;
using System.Reflection;

namespace NewRelic.Agent.Core.CrossAgentTests.DataTransport
{
	internal class TestDefaultConfiguration : DefaultConfiguration
	{
		public TestDefaultConfiguration([NotNull] IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic) : base(environment, localConfig, serverConfig, runTimeConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic) { }
	}

	[TestFixture]
	public class CollectorHostNameTests
    {
		[NotNull]
		private IEnvironment _environment;

		[NotNull]
		private IProcessStatic _processStatic;

		[NotNull]
		private IHttpRuntimeStatic _httpRuntimeStatic;

		[NotNull]
		private IConfigurationManagerStatic _configurationManagerStatic;

		[NotNull]
		private configuration _localConfig;

		[NotNull]
		private ServerConfiguration _serverConfig;

		[NotNull]
		private RunTimeConfiguration _runTimeConfig;

		[NotNull]
		private DefaultConfiguration _defaultConfig;

		public static List<TestCaseData> CollectorHostnameTestData
		{
			get { return GetCollectorHostnameTestData(); }
		}

		[SetUp]
		public void Setup()
		{
			_environment = Mock.Create<IEnvironment>();
			_processStatic = Mock.Create<IProcessStatic>();
			_httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
			_configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
			_localConfig = new configuration();
			_serverConfig = new ServerConfiguration();
			_runTimeConfig = new RunTimeConfiguration();
			_defaultConfig = new TestDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

		}

		[TestCaseSource("CollectorHostnameTestData")]
		public void RunCrossAgentCollectorHostnameTests(string configFileKey, string envKey, string configOverrideHost, string envOverrideHost, string hostname)
		{
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey")).Returns<string>(null);

			if (envKey != null)
			{
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns(envKey);
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns(envKey);
			}
			else
			{
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns<string>(null);
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns<string>(null);
			}

			if (envOverrideHost != null)
			{
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_HOST")).Returns(envOverrideHost);
			}
			else
			{
				Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_HOST")).Returns<string>(null);
			}

			if (configFileKey != null)
			{
				_localConfig.service.licenseKey = configFileKey;
			}

			if (configOverrideHost != null)
			{
				_localConfig.service.host = configOverrideHost;
			}

			var connectionInfo = new ConnectionInfo(_defaultConfig);
			Assert.AreEqual(hostname, connectionInfo.Host);
		}

		private static List<TestCaseData> GetCollectorHostnameTestData()
		{
			var testDatas = new List<TestCaseData>();
			var dllPath = Path.GetDirectoryName(new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath);
			var jsonPath = Path.Combine(dllPath, "CrossAgentTests", "DataTransport", "collector_hostname.json");
			var jsonString = File.ReadAllText(jsonPath);
			var objectArray = JArray.Parse(jsonString);

			foreach (var obj in objectArray) 
			{
				var testData = new TestCaseData(new string[] { (string)obj["config_file_key"], (string)obj["env_key"], (string)obj["config_override_host"], (string)obj["env_override_host"], (string)obj["hostname"] });
				testData.SetName("RunCrossAgentCollectorHostnameTests: " + (string)obj["name"]);
				testDatas.Add(testData);
			}

			return testDatas;
		}
	}
}

