using System;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.Logging;
using NUnit.Framework;

namespace NewRelic.Agent.Core
{

	[TestFixture]
	public class LoggerBootstrapperTest
	{
		[Test]
		public static void IsDebugEnabled_is_false_when_config_log_is_off()
		{
			ILogConfig config = GetLogConfig("off");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.IsFalse(Log.IsDebugEnabled);
		}

		[Test]
		public static void IsDebugEnabled_is_true_when_config_log_is_all()
		{
			ILogConfig config = GetLogConfig("all");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.That(Log.IsDebugEnabled);
		}

		[Test]
		public static void IsInfoEnabled_is_true_when_config_log_is_info()
		{
			ILogConfig config = GetLogConfig("info");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.That(Log.IsInfoEnabled);
		}

		[Test]
		public static void IsDebugEnabled_is_false_when_config_log_is_info()
		{
			ILogConfig config = GetLogConfig("info");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.IsFalse(Log.IsDebugEnabled);
		}

		[Test]
		public static void IsDebugEnabled_is_true_when_config_log_is_debug()
		{
			ILogConfig config = GetLogConfig("debug");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.That(Log.IsDebugEnabled);
		}

		[Test]
		public static void IsEnabledFor_finest_is_false_when_config_log_is_debug()
		{
			ILogConfig config = GetLogConfig("debug");
			LoggerBootstrapper.Initialize();
			LoggerBootstrapper.ConfigureLogger(config);
			Assert.IsFalse(Log.IsFinestEnabled);

		}

		[Test]
		public static void Config_IsAuditEnabled_for_config_is_true_when_auditLog_true_in_config()
		{
			ILogConfig config = LogConfigFixtureWithAuditLogEnabled("debug");
			Assert.That(config.IsAuditLogEnabled);
		}

		[Test]
		public static void Config_IsAuditEnabled_for_config_is_false_when_not_added_to_config()
		{
			ILogConfig config = GetLogConfig("debug");
			Assert.IsFalse(config.IsAuditLogEnabled);
		}


		static private ILogConfig GetLogConfig(string logLevel)
		{
			var xml = String.Format(
				"<configuration xmlns=\"urn:newrelic-config\">" +
				"   <service licenseKey=\"dude\"/>" +
				"   <application>" +
				"       <name>Test</name>" +
				"   </application>" +
				"   <log level=\"{0}\"/>" +
				"</configuration>",
				logLevel);
			var configuration = ConfigurationLoader.InitializeFromXml(xml);
			return configuration.LogConfig;
		}

		static private ILogConfig LogConfigFixtureWithAuditLogEnabled(string logLevel)
		{
			var xml = String.Format(
				"<configuration xmlns=\"urn:newrelic-config\">" +
				"   <service licenseKey=\"dude\"/>" +
				"   <application>" +
				"       <name>Test</name>" +
				"   </application>" +
				"   <log level=\"{0}\" auditLog=\"true\"/>" + 
				"</configuration>",
				logLevel);
			var configuration = ConfigurationLoader.InitializeFromXml(xml);
			return configuration.LogConfig;
		}
	}
}
