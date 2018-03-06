using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Config;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Configuration.UnitTest
{
	internal class DefaultConfigurationTest : DefaultConfiguration
	{
		public DefaultConfigurationTest([NotNull] IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic) : base(environment, localConfig, serverConfig, runTimeConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic) { }
	}

	[TestFixture, Category("Configuration")]
	public class Class_DefaultConfiguration
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

		[SetUp]
		public void SetUp()
		{
			_environment = Mock.Create<IEnvironment>();
			_processStatic = Mock.Create<IProcessStatic>();
			_httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
			_configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
			_localConfig = new configuration();
			_serverConfig = new ServerConfiguration();
			_runTimeConfig = new RunTimeConfiguration();
			_defaultConfig = new DefaultConfigurationTest(_environment, _localConfig, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);
		}

		[Test]
		public void AgentEnabled_passes_through_to_local_config()
		{
			Assert.IsTrue(_defaultConfig.AgentEnabled);
			_localConfig.agentEnabled = true;
			Assert.IsTrue(_defaultConfig.AgentEnabled);
			_localConfig.agentEnabled = false;
			Assert.IsFalse(_defaultConfig.AgentEnabled);
		}

		[TestCase(null, null, null, ExpectedResult = true)]
		[TestCase(null, null, true, ExpectedResult = true)]
		[TestCase(null, null, false, ExpectedResult = false)]
		[TestCase(null, true, null, ExpectedResult = true)]
		[TestCase(null, true, true, ExpectedResult = true)]
		[TestCase(null, true, false, ExpectedResult = false)]
		[TestCase(null, false, null, ExpectedResult = false)]
		[TestCase(null, false, true, ExpectedResult = true)]
		[TestCase(null, false, false, ExpectedResult = false)]
		[TestCase(true, null, null, ExpectedResult = true)]
		[TestCase(true, null, true, ExpectedResult = true)]
		[TestCase(true, null, false, ExpectedResult = false)]
		[TestCase(true, true, null, ExpectedResult = true)]
		[TestCase(true, true, true, ExpectedResult = true)]
		[TestCase(true, true, false, ExpectedResult = false)]
		[TestCase(true, false, null, ExpectedResult = false)]
		[TestCase(true, false, true, ExpectedResult = true)]
		[TestCase(true, false, false, ExpectedResult = false)]
		[TestCase(false, null, null, ExpectedResult = false)]
		[TestCase(false, null, true, ExpectedResult = false)]
		[TestCase(false, null, false, ExpectedResult = false)]
		[TestCase(false, true, null, ExpectedResult = false)]
		[TestCase(false, true, true, ExpectedResult = false)]
		[TestCase(false, true, false, ExpectedResult = false)]
		[TestCase(false, false, null, ExpectedResult = false)]
		[TestCase(false, false, true, ExpectedResult = false)]
		[TestCase(false, false, false, ExpectedResult = false)]
		public Boolean Property__TransactionEvents_server_can_disable(Boolean? server, Boolean? legacyLocal, Boolean? local)
		{
			_localConfig.transactionEvents.enabled = (local != null) ? local.Value : default(Boolean);
			_localConfig.transactionEvents.enabledSpecified = local.HasValue;

			_localConfig.analyticsEvents.enabled = (legacyLocal != null) ? legacyLocal.Value : default(Boolean);
			_localConfig.analyticsEvents.enabledSpecified = legacyLocal.HasValue;

			_serverConfig.AnalyticsEventCollectionEnabled = server;

			return _defaultConfig.TransactionEventsEnabled;
		}

		[Test]
		public void every_config_gets_new_version_number()
		{
			var newConfig = new DefaultConfigurationTest(_environment, _localConfig, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.AreEqual(_defaultConfig.ConfigurationVersion, newConfig.ConfigurationVersion - 1);
		}

		[Test]
		public void when_configs_are_default_then_TransactionEvents_are_enabled()
		{

			Assert.IsTrue(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void when_configs_are_default_then_PutForDataSend_is_disabled()
		{
			Assert.IsFalse(_defaultConfig.PutForDataSend);
		}

		[Test]
		public void when_configs_are_default_then_InstanceReportingEnabled_is_disabled()
		{
			Assert.IsTrue(_defaultConfig.InstanceReportingEnabled);
		}

		[Test]
		public void when_configs_are_default_then_DatabaseNameReportingEnabled_is_disabled()
		{
			Assert.IsTrue(_defaultConfig.DatabaseNameReportingEnabled);
		}

		[Test]
		public void CompressedContentEncodingShouldBeDeflateWhenConfigsAreDefault()
		{
			Assert.AreEqual("deflate", _defaultConfig.CompressedContentEncoding);
		}

		[Test]
		public void when_TransactionEvents_are_enabled_in_local_config_and_do_not_exist_in_server_config_then_TransactionEvents_are_enabled()
		{
			_localConfig.transactionEvents.enabled = true;
			Assert.IsTrue(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void when_TransactionEvents_are_disabled_in_local_config_and_do_not_exist_in_server_config_then_TransactionEvents_are_disabled()
		{
			_localConfig.transactionEvents.enabled = false;
			Assert.IsFalse(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void TransactionEventsMaxSamplesPerMinute_is_capped_at_10000()
		{

			Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaxSamplesPerMinute);

			_localConfig.transactionEvents.maximumSamplesPerMinute = 10001;
			Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaxSamplesPerMinute);

			_localConfig.transactionEvents.maximumSamplesPerMinute = 9999;
			Assert.AreEqual(9999, _defaultConfig.TransactionEventsMaxSamplesPerMinute);
		}

		[Test]
		public void TransactionEventsMaxSamplesStored_passes_through_to_local_config()
		{

			Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaxSamplesStored);

			_localConfig.transactionEvents.maximumSamplesStored = 10001;
			Assert.AreEqual(10001, _defaultConfig.TransactionEventsMaxSamplesStored);

			_localConfig.transactionEvents.maximumSamplesStored = 9999;
			Assert.AreEqual(9999, _defaultConfig.TransactionEventsMaxSamplesStored);
		}

		[TestCase(true, null, null, ExpectedResult = true)]
		[TestCase(true, null, true, ExpectedResult = true)]
		[TestCase(true, null, false, ExpectedResult = false)]
		[TestCase(true, true, true, ExpectedResult = true)]
		[TestCase(true, false, true, ExpectedResult = false)]
		[TestCase(true, true, false, ExpectedResult = false)]
		[TestCase(true, false, null, ExpectedResult = false)]
		[TestCase(true, true, null, ExpectedResult = true)]
		[TestCase(false, null, null, ExpectedResult = false)]
		[TestCase(false, null, true, ExpectedResult = true)]
		[TestCase(false, null, false, ExpectedResult = false)]
		[TestCase(false, true, true, ExpectedResult = true)]
		[TestCase(false, false, true, ExpectedResult = false)]
		[TestCase(false, true, false, ExpectedResult = false)]
		[TestCase(false, false, null, ExpectedResult = false)]
		[TestCase(false, true, null, ExpectedResult = false)]
		public Boolean Property__ErrorCollectorEnabled_featue_with_rpm_collector_enabled_server_overrides(Boolean local, Boolean? server, Boolean? rpmConfigServer)
		{
			_localConfig.errorCollector.enabled = local;
			_serverConfig.ErrorCollectionEnabled = server;
			_serverConfig.RpmConfig.ErrorCollectorEnabled = rpmConfigServer;

			return _defaultConfig.ErrorCollectorEnabled;
		}

		[Test]
		public void Property_ErrorsMaximumPerPeriod_returns_static_20()
		{
			Assert.AreEqual(20, _defaultConfig.ErrorsMaximumPerPeriod);
		}

		[Test]
		public void Property_SqlTracesPerPeriod_returns_static_10()
		{
			Assert.AreEqual(10, _defaultConfig.SqlTracesPerPeriod);
		}

		[Test]
		public void Property_SlowSql_server_overrides_when_set()
		{
			_serverConfig.RpmConfig.SlowSqlEnabled = true;
			_localConfig.slowSql.enabled = false;

			Assert.AreEqual(true, _defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void Property_SlowSql_server_overrides_when_local_is_default()
		{
			_serverConfig.RpmConfig.SlowSqlEnabled = false;

			Assert.AreEqual(false, _defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void Property_SlowSql_default_is_true()
		{
			Assert.IsTrue(_defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void Property_SlowSql_local_config_set_to_false()
		{
			_localConfig.slowSql.enabled = false;
			Assert.IsFalse(_defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void when_Property_StackTraceMaximumFrames_is_set()
		{
			_localConfig.maxStackTraceLines = 100;
			Assert.AreEqual(100, _defaultConfig.StackTraceMaximumFrames);
		}

		[TestCase(null, ExpectedResult = 80)]
		[TestCase(100, ExpectedResult = 100)]
		public int Property_StackTraceMaximumFrames_set_from_local(int? maxFrames)
		{
			var value = maxFrames ?? 80;
			_localConfig.maxStackTraceLines = value;
			return _defaultConfig.StackTraceMaximumFrames;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_InstrumentationLoggingEnabled_set_from_local(Boolean local)
		{
			_localConfig.instrumentation.log = local;

			return _defaultConfig.InstrumentationLoggingEnabled;
		}

		[TestCase(true, null, null, ExpectedResult = true)]
		[TestCase(true, null, true, ExpectedResult = true)]
		[TestCase(true, null, false, ExpectedResult = false)]
		[TestCase(true, true, true, ExpectedResult = true)]
		[TestCase(true, false, true, ExpectedResult = false)]
		[TestCase(true, true, false, ExpectedResult = false)]
		[TestCase(true, false, null, ExpectedResult = false)]
		[TestCase(true, true, null, ExpectedResult = true)]
		[TestCase(false, null, null, ExpectedResult = false)]
		[TestCase(false, null, true, ExpectedResult = true)]
		[TestCase(false, null, false, ExpectedResult = false)]
		[TestCase(false, true, true, ExpectedResult = true)]
		[TestCase(false, false, true, ExpectedResult = false)]
		[TestCase(false, true, false, ExpectedResult = false)]
		[TestCase(false, false, null, ExpectedResult = false)]
		[TestCase(false, true, null, ExpectedResult = false)]
		public Boolean Property__TransactionTracerEnabled_feature_with_rpm_collector_enabled_server_overrides(Boolean local, Boolean? server, Boolean? rpmConfigServer)
		{
			_localConfig.transactionTracer.enabled = local;
			_serverConfig.TraceCollectionEnabled = server;
			_serverConfig.RpmConfig.TransactionTracerEnabled = rpmConfigServer;

			return _defaultConfig.TransactionTracerEnabled;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_TransactionTracerCaptureAttributes_set_from_local(Boolean local)
		{
			_localConfig.transactionTracer.captureAttributes = local;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_DataTransmissionPutForDataSend_set_from_local(Boolean local)
		{
			_localConfig.dataTransmission.putForDataSend = local;

			return _defaultConfig.PutForDataSend;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_DatastoreTracerInstanceReportingEnabled_set_from_local(Boolean local)
		{
			_localConfig.datastoreTracer.instanceReporting.enabled = local;

			return _defaultConfig.InstanceReportingEnabled;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_DatastoreTracerDatabaseNameReportingEnabled_set_from_local(Boolean local)
		{
			_localConfig.datastoreTracer.databaseNameReporting.enabled = local;

			return _defaultConfig.DatabaseNameReportingEnabled;
		}

		[TestCase(configurationDataTransmissionCompressedContentEncoding.deflate, ExpectedResult = "deflate")]
		[TestCase(configurationDataTransmissionCompressedContentEncoding.gzip, ExpectedResult = "gzip")]
		public String CompressedContentEncodingShouldSetFromLocalConfiguration(configurationDataTransmissionCompressedContentEncoding local)
		{
			_localConfig.dataTransmission.compressedContentEncoding = local;
			return _defaultConfig.CompressedContentEncoding;
		}


		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_ErrorCollectorCatpureEvents_set_from_local(Boolean local)
		{
			_localConfig.errorCollector.captureEvents = local;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public Boolean Property_ErrorCollectorCatpureEvents_set_from_server(Boolean local, Boolean server)
		{
			_localConfig.errorCollector.captureEvents = local;
			_serverConfig.RpmConfig.ErrorCollectorCaptureEvents = server;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		public Boolean Property_ErrorCollectorCaptureEvents_conditional_override_from_server(Boolean local, Boolean server)
		{
			_localConfig.errorCollector.captureEvents = local;
			_serverConfig.RpmConfig.CollectErrorEvents = server;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(50, ExpectedResult = 50)]
		public UInt32 Property_ErrorCollectorMaxNumberEventSamples_set_from_local(Int32 local)
		{
			_localConfig.errorCollector.maxEventSamplesStored = local;
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(50, 75, ExpectedResult = 75)]
		public UInt32 Property_ErrorCollectorMaxNumberEventSamples_set_from_server(Int32 local, Int32 server)
		{
			_localConfig.errorCollector.maxEventSamplesStored = Property_InstrumentationLevel_server_overrides_default(local);
			_serverConfig.RpmConfig.ErrorCollectorMaxEventSamplesStored = (UInt32?)server;
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(ExpectedResult = 100)]
		public UInt32 Property_ErrorCollectorMaxNumberEventSamples_default_from_local()
		{
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_BrowserMonitoringCaptureAttributes_set_from_local(Boolean local)
		{
			_localConfig.browserMonitoring.captureAttributes = local;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[TestCase(null, ExpectedResult = 3)]
		[TestCase(42, ExpectedResult = 42)]
		public Int32 Property_InstrumentationLevel_server_overrides_default(Int32? server)
		{
			_serverConfig.RpmConfig.InstrumentationLevel = server;

			return _defaultConfig.InstrumentationLevel;
		}

		[TestCase(true, null, ExpectedResult = true)]
		[TestCase(false, null, ExpectedResult = false)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		public Boolean Property_SqlExplainPlansEnabled__server_overrides_local(Boolean local, Boolean? server)
		{
			_localConfig.transactionTracer.explainEnabled = local;
			_serverConfig.RpmConfig.TransactionTracerExplainEnabled = server;

			return _defaultConfig.SqlExplainPlansEnabled;
		}

		[TestCase(3000.0, null, ExpectedResult = 3000.0)]
		[TestCase(3000.5, null, ExpectedResult = 3001.0)]
		[TestCase(4000.0, 0.5, ExpectedResult = 500.0)]
		[TestCase(200.0, 5.0, ExpectedResult = 5000.0)]
		[TestCase(1.0, 0.2, ExpectedResult = 200.0)]
		public double Property_ExplainPlanThreshold_set_from_server_overrides_local(double local, double? server)
		{
			_localConfig.transactionTracer.explainThreshold = (float)local;
			if (server != null)
			{ 
				_serverConfig.RpmConfig.TransactionTracerExplainThreshold = server;
			}

			var configValue = _defaultConfig.SqlExplainPlanThreshold;
			return configValue.TotalMilliseconds;
		}

		[TestCase(0.5, ExpectedResult = 500.0)]
		[TestCase(1.0, ExpectedResult = 1000.0)]
		[TestCase(1.5, ExpectedResult = 1500.0)]
		public double ServerSideExplainPlanThresholdShouldConvertFromSeconds(double serverThreshold)
		{
			_serverConfig.RpmConfig.TransactionTracerExplainThreshold = serverThreshold;

			var configValue = _defaultConfig.SqlExplainPlanThreshold;
			return configValue.TotalMilliseconds;
		}

		[TestCase(100.0, ExpectedResult = 100.0)]
		[TestCase(1500.0, ExpectedResult = 1500.0)]
		[TestCase(3000.0, ExpectedResult = 3000.0)]
		public double LocalExplainPlanThresholdShouldConvertFromMilliseconds(double localThreshold)
		{
			_localConfig.transactionTracer.explainThreshold = (float)localThreshold;

			var configValue = _defaultConfig.SqlExplainPlanThreshold;
			return configValue.TotalMilliseconds;
		}

		[Test]
		public void Property_SqlStatementsPerTransaction__always_returns_500()
		{
			Assert.AreEqual(500, _defaultConfig.SqlStatementsPerTransaction);
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, null, ExpectedResult = false)]
		public bool Property_TransactionEventsEnabled_set_from_local_and_server_ServerCanDisable(bool local, bool? server)
		{
			_serverConfig.AnalyticsEventCollectionEnabled = server;
			_localConfig.transactionEvents.enabled = local;

			return _defaultConfig.TransactionEventsEnabled;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, null, ExpectedResult = false)]
		public bool Property_CaptureParameters_set_from_local_and_server_ServerOverrides(bool local, bool? server)
		{
			_serverConfig.RpmConfig.CaptureParametersEnabled = server;
			_localConfig.transactionEvents.enabled = local;

			return _defaultConfig.CaptureRequestParameters;
		}

		[TestCase(new[] {"local"}, new[] {"server"}, "request.parameters.server")]
		[TestCase(new[] {"local"}, null, "request.parameters.local")]
		public void Property_RequestParametersToIgnore_set_from_local_and_server_ServerOverrides(string[] local, string[] server, string expected)
		{
			_serverConfig.RpmConfig.ParametersToIgnore = server;
			_localConfig.requestParameters.ignore = new List<String>(local);

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains(expected));
		}

		[TestCase(false, configurationTransactionTracerRecordSql.obfuscated, null, ExpectedResult = "obfuscated")]
		[TestCase(false, configurationTransactionTracerRecordSql.off, null, ExpectedResult = "off")]
		[TestCase(false, configurationTransactionTracerRecordSql.raw, null, ExpectedResult = "raw")]
		[TestCase(false, configurationTransactionTracerRecordSql.obfuscated, "foo", ExpectedResult = "foo")]
		[TestCase(false, configurationTransactionTracerRecordSql.off, "foo", ExpectedResult = "foo")]
		[TestCase(false, configurationTransactionTracerRecordSql.raw, "foo", ExpectedResult = "foo")]
		[TestCase(true, configurationTransactionTracerRecordSql.off, null, ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, null, ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.raw, null, ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.off, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.raw, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.off, "foo", ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, "foo", ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.raw, "foo", ExpectedResult = "obfuscated")]
		public String Property__TransactionTracerRecordSql__set_from_local_and_server_HighSecurityOverridesServerOverrides(Boolean highSecurity, configurationTransactionTracerRecordSql local, String server)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.transactionTracer.recordSql = local;
			_serverConfig.RpmConfig.TransactionTracerRecordSql = server;

			return _defaultConfig.TransactionTracerRecordSql;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, null, ExpectedResult = true)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, null, ExpectedResult = false)]
		public bool Property_HighSecurity_set_from_local_overrides_server(bool local, bool? server)
		{
			_localConfig.highSecurity.enabled = local;
			_serverConfig.HighSecurityEnabled = server;

			return _defaultConfig.HighSecurityModeEnabled;
		}

		[TestCase(true, true, ExpectedResult = false)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public bool Property_LiveInstrumentation_HighSecurityOverrides(bool highSecurity, bool liveInstrumentation)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.liveInstrumentation.enabled = liveInstrumentation;

			return _defaultConfig.LiveInstrumentationEnabled;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public bool Property_StripExceptionMessages_HighSecurityOverrides(bool highSecurity, bool stripErrorMessages)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.stripExceptionMessages.enabled = stripErrorMessages;

			return _defaultConfig.StripExceptionMessages;
		}

		[Test]
		public void Property_UseSsl_overridden_by_local_HighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.browserMonitoring.sslForHttp = false;

			Assert.IsTrue(_defaultConfig.BrowserMonitoringUseSsl);
		}

		[Test]
		public void Property_CaptureCustomParameters_overridden_by_local_HighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.parameterGroups.customParameters.enabled = true;

			Assert.IsFalse(_defaultConfig.CaptureCustomParameters);
		}

		[Test]
		public void Property_CaptureRequestParameters_overridden_by_local_HighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.requestParameters.enabled = true;

			Assert.IsFalse(_defaultConfig.CaptureRequestParameters);
		}

		[Test]
		public void Property_RecordSql_overridden_by_local_HighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.transactionTracer.recordSql = configurationTransactionTracerRecordSql.raw;

			Assert.AreEqual(_defaultConfig.TransactionTracerRecordSql, configurationTransactionTracerRecordSql.obfuscated.ToString());
		}

		[TestCase("apdex_f", null, 5, ExpectedResult = 20000)]
		[TestCase("1", null, 5, ExpectedResult = 1)]
		[TestCase("1.5", null, 5, ExpectedResult = 2)]
		[TestCase("apdex_f", 3, 5, ExpectedResult = 3000)]
		[TestCase("apdex_f", 3.5, 5, ExpectedResult = 3500)]
		[TestCase("apdex_f", "4", 5, ExpectedResult = 4000)]
		[TestCase("apdex_f", "4.5", 5, ExpectedResult = 4500)]
		[TestCase("apdex_f", "apdex_f", 5, ExpectedResult = 20000)]
		[TestCase("apdex_f", "foo", 5, ExpectedResult = 20000)]
		[TestCase(null, null, 5, ExpectedResult = 20000)]
		[TestCase(null, 2, 5, ExpectedResult = 2000)]
		public Double Property_TransactionTraceThreshold_set_from_server_overrides_local(String local, Object server, Double apdexT)
		{
			_serverConfig.ApdexT = apdexT;
			_serverConfig.RpmConfig.TransactionTracerThreshold = server;
			_localConfig.transactionTracer.transactionThreshold = local;

			return _defaultConfig.TransactionTraceThreshold.TotalMilliseconds;
		}

		[Test]
		public void when_TransactionTraceThreshold_set_to_apdex_f_then_equals_apdex_t_times_4()
		{
			_serverConfig.ApdexT = 42;
			_localConfig.transactionTracer.transactionThreshold = "apdex_f";

			Assert.AreEqual(42*4, _defaultConfig.TransactionTraceThreshold.TotalSeconds);
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_CaptureCustomParameters_set_from_local(Boolean isEnabled)
		{
			_localConfig.parameterGroups.customParameters.enabled = isEnabled;
			return _defaultConfig.CaptureCustomParameters;
		}

		[Test]
		public void Property_CaptureCustomParameters_set_from_local_defaults_to_true()
		{
			Assert.IsTrue(_defaultConfig.CaptureCustomParameters);
		}

		[Test]
		public void Property_CustomParametersToIgnore_set_from_local()
		{
			_localConfig.parameterGroups.customParameters.ignore = new List<String>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("local"));
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public Boolean Property_CaptureIdentityParameters_set_from_local(Boolean isEnabled)
		{
			_localConfig.parameterGroups.identityParameters.enabled = isEnabled;
			return _defaultConfig.CaptureErrorCollectorAttributesIncludes.Contains("identity.*");
		}

		[Test]
		public void Property_CaptureIdentityParameters_set_from_local_defaults_to_false()
		{
			Assert.IsTrue(_defaultConfig.CaptureAttributesDefaultExcludes.Contains("identity.*"));
		}

		[Test]
		public void Property_IdentityParametersToIgnore_set_from_local()
		{

			_localConfig.parameterGroups.identityParameters.ignore = new List<String>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("identity.local"));
		}

		[TestCase(true, ExpectedResult = false)]
		[TestCase(false, ExpectedResult = true)]
		public Boolean Property_CaptureResponseHeaderParameters_set_from_local(Boolean isEnabled)
		{
			_localConfig.parameterGroups.responseHeaderParameters.enabled = isEnabled;
			return _defaultConfig.CaptureAttributesExcludes.Contains("response.headers.*");
		}

		[Test]
		public void Property_CaptureResponseHeaderParameters_set_from_local_defaults_to_true()
		{
			Assert.IsFalse(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.*"));
		}

		[Test]
		public void Property_ResponseHeaderParametersToIgnore_set_from_local()
		{
			_localConfig.parameterGroups.responseHeaderParameters.ignore = new List<String>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.local"));
		}

		[TestCase(new[] {"local"}, new[] {"server"}, ExpectedResult = "server")]
		[TestCase(new[] {"local"}, null, ExpectedResult = "local")]
		public string Property_ExceptionsToIgnore_set_from_local_and_server_ServerOverrides(string[] local, string[] server)
		{
			_serverConfig.RpmConfig.ErrorCollectorErrorsToIgnore = server;
			_localConfig.errorCollector.ignoreErrors.exception = new List<String>(local);

			return _defaultConfig.ExceptionsToIgnore.FirstOrDefault();
		}

		[TestCase("local", "server", ExpectedResult = "server")]
		[TestCase("local", null, ExpectedResult = "local")]
		[TestCase("local", "", ExpectedResult = "")] //If server sends back string.empty then override with that
		public string Property_BrowserMonitoringJavaScriptAgentLoaderType_set_from_local_and_server_ServerOverrides(string local, string server)
		{
			_serverConfig.RumSettingsBrowserMonitoringLoader = server;
			_localConfig.browserMonitoring.loader = local;

			return _defaultConfig.BrowserMonitoringJavaScriptAgentLoaderType;
		}

		[TestCase(new float[] {400, 404}, new[] {"500"}, ExpectedResult = "500")]
		[TestCase(new float[] {400, 404}, null, ExpectedResult = "400")]
		public string Property_StatusCodesToIgnore_set_from_local_and_server_ServerOverrides(float[] local, string[] server)
		{
			_serverConfig.RpmConfig.ErrorCollectorStatusCodesToIgnore = server;
			_localConfig.errorCollector.ignoreStatusCodes.code = new List<float>(local);

			return _defaultConfig.HttpStatusCodesToIgnore.FirstOrDefault();
		}

		[Test]
		public void Static_Field__Instance__is_not_null()
		{
			Assert.NotNull(DefaultConfiguration.Instance);
		}

		[Test]
		public void Static_Property_RequestPathBlacklist_is_not_null()
		{
			Assert.NotNull(DefaultConfiguration.Instance.RequestPathExclusionList);
		}

		[Test]
		public void Property_RequestPathBlackList_empty_if_no_request_paths_in_blacklist()
		{
			Assert.IsEmpty(DefaultConfiguration.Instance.RequestPathExclusionList);
		}

		[Test]
		public void Property_RequestPathBlackList_contains_one_entry()
		{
			var path = new configurationBrowserMonitoringPath();
			path.regex = "one";

			_localConfig.browserMonitoring.requestPathsExcluded.Add(path);

			Assert.AreEqual(1, _defaultConfig.RequestPathExclusionList.Count());
		}

		[Test]
		public void Property_RequestPathBlackList_contains_two_entries()
		{
			var path1 = new configurationBrowserMonitoringPath();
			path1.regex = "one";

			var path2 = new configurationBrowserMonitoringPath();
			path2.regex = "two";

			_localConfig.browserMonitoring.requestPathsExcluded.Add(path1);
			_localConfig.browserMonitoring.requestPathsExcluded.Add(path2);

			Assert.AreEqual(2, _defaultConfig.RequestPathExclusionList.Count());
		}

		[Test]
		public void Property_RequestPathBlackList_bad_regex()
		{
			var path = new configurationBrowserMonitoringPath();
			path.regex = ".*(?<!\\)\\(?!\\).*";

			_localConfig.browserMonitoring.requestPathsExcluded.Add(path);

			Assert.AreEqual(0, _defaultConfig.RequestPathExclusionList.Count());
		}

		[Test]
		public void Property_BrowserMonitoringJavaScriptAgentLoaderType_set_to_default_rum()
		{
			Assert.AreEqual("rum", _defaultConfig.BrowserMonitoringJavaScriptAgentLoaderType);
		}

		[TestCase(500, null, ExpectedResult = 500)]
		[TestCase(1, null, ExpectedResult = 1)]
		[TestCase(0, null, ExpectedResult = 0)]
		[TestCase(500, 0.5, ExpectedResult = 500)]
		[TestCase(500, 0.0, ExpectedResult = 0)]
		[TestCase(0, 0.5, ExpectedResult = 500)]
		[TestCase(1, 0.2, ExpectedResult = 200)]
		[TestCase(-300, null, ExpectedResult = -300)]
		public int Property_TransactionTracerStackThreshold_server_overrides_local(int local, double? server)
		{
			_localConfig.transactionTracer.stackTraceThreshold = local;
			_serverConfig.RpmConfig.TransactionTracerStackThreshold = server;

			return _defaultConfig.TransactionTracerStackThreshold.Milliseconds;
		}

		[Test]
		public void Property_ThreadProfilingIgnoreMethod_from_xml_decodes_into_list_of_strings()
		{
			const string xmlString = @"<?xml version=""1.0""?>
<configuration xmlns=""urn:newrelic-config"" agentEnabled=""true"">
  <service licenseKey=""REPLACE_WITH_LICENSE_KEY"" ssl=""true"" />
  <threadProfiling>
	<ignoreMethod>System.Threading.WaitHandle:WaitOne</ignoreMethod>
	<ignoreMethod>System.Threading.WaitHandle:WaitAny</ignoreMethod>
	<ignoreMethod>Microsoft.Samples.Runtime.Remoting.Channels.Pipe.PipeConnection:WaitForConnect</ignoreMethod>
  </threadProfiling>
</configuration>";
			var root = new XmlRootAttribute {ElementName = "configuration", Namespace = "urn:newrelic-config"};
			var serializer = new XmlSerializer(typeof (configuration), root);

			configuration localConfiguration;
			using (var reader = new StringReader(xmlString))
			{
				localConfiguration = serializer.Deserialize(reader) as configuration;
			}

			_defaultConfig = new DefaultConfigurationTest(_environment, localConfiguration, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsTrue(_defaultConfig.ThreadProfilingIgnoreMethods.Contains("System.Threading.WaitHandle:WaitAny"));
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool Property_BrowserMonitoring_overrides_deprecated_value(Boolean propertyEnabled, Boolean deprecatedEnabled)
		{
			_localConfig.browserMonitoring.captureAttributes = deprecatedEnabled;
			_localConfig.browserMonitoring.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool Property_BrowserMonitoring_deprecated_value_overrides_default(Boolean deprecatedEnabled)
		{
			_localConfig.browserMonitoring.captureAttributesSpecified = false;
			_localConfig.browserMonitoring.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[Test]
		public void Property_BrowserMonitoring_uses_default_when_no_config_values()
		{
			_localConfig.browserMonitoring.captureAttributesSpecified = false;
			_localConfig.browserMonitoring.attributes.enabledSpecified = false;

			Assert.IsFalse(_defaultConfig.CaptureBrowserMonitoringAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool Property_ErrorCollector_overrides_deprecated_value(Boolean propertyEnabled, Boolean deprecatedEnabled)
		{
			_localConfig.errorCollector.captureAttributes = deprecatedEnabled;
			_localConfig.errorCollector.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureErrorCollectorAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool Property_ErrorCollector_deprecated_value_overrides_default(Boolean deprecatedEnabled)
		{
			_localConfig.errorCollector.captureAttributesSpecified = false;
			_localConfig.errorCollector.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureErrorCollectorAttributes;
		}

		[Test]
		public void Property_ErrorCollector_uses_default_when_no_config_values()
		{
			_localConfig.errorCollector.captureAttributesSpecified = false;
			_localConfig.errorCollector.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureErrorCollectorAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool Property_TransactionTracer_overrides_deprecated_value(Boolean propertyEnabled, Boolean deprecatedEnabled)
		{
			_localConfig.transactionTracer.captureAttributes = deprecatedEnabled;
			_localConfig.transactionTracer.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool Property_TransactionTracer_deprecated_value_overrides_default(Boolean deprecatedEnabled)
		{
			_localConfig.transactionTracer.captureAttributesSpecified = false;
			_localConfig.transactionTracer.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[Test]
		public void Property_TransactionTracer_uses_default_when_no_config_values()
		{
			_localConfig.transactionTracer.captureAttributesSpecified = false;
			_localConfig.transactionTracer.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureTransactionTraceAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool Property_TransactionEvent_overrides_deprecated_value(Boolean propertyEnabled, Boolean deprecatedEnabled)
		{
			_localConfig.analyticsEvents.captureAttributes = deprecatedEnabled;
			_localConfig.transactionEvents.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureTransactionEventsAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool Property_AnalyticsEvent_deprecated_value_overrides_default(Boolean deprecatedEnabled)
		{
			_localConfig.analyticsEvents.captureAttributesSpecified = false;
			_localConfig.transactionEvents.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureTransactionEventsAttributes;
		}

		[Test]
		public void Property_TransactionEvent_uses_default_when_no_config_values()
		{
			_localConfig.analyticsEvents.captureAttributesSpecified = false;
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureTransactionEventsAttributes);
		}

		[Test]
		public void Property_deprecated_ignore_identityParameters_value_becomes_exclude()
		{
			_localConfig.parameterGroups.identityParameters.ignore = new List<String>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("identity.foo"));
		}

		[Test]
		public void Property_deprecated_ignore_customParameters_value_becomes_exclude()
		{
			_localConfig.parameterGroups.customParameters.ignore = new List<String>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("foo"));
		}

		[Test]
		public void Property_deprecated_ignore_responseHeaderParameters_value_becomes_exclude()
		{
			_localConfig.parameterGroups.responseHeaderParameters.ignore = new List<String>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.foo"));
		}

		[Test]
		public void Property_deprecated_ignore_requestHeaderParameters_value_becomes_exclude()
		{
			_localConfig.parameterGroups.requestHeaderParameters.ignore = new List<String>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("request.headers.foo"));
		}

		[Test]
		public void Property_deprecated_ignore_requestParameters_value_becomes_exclude()
		{
			_localConfig.requestParameters.ignore = new List<String>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("request.parameters.foo"));
		}

		[TestCase(null, null, ExpectedResult = null)]
		[TestCase(null, "Foo", ExpectedResult = "Foo")]
		[TestCase("Foo", null, ExpectedResult = "Foo")]
		[TestCase("Foo", "Bar", ExpectedResult = "Foo")]
		public String Property__Labels__environment_overrides_local(String environment, String local)
		{
			_localConfig.labels = local;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LABELS")).Returns(environment);

			return _defaultConfig.Labels;
		}

		[TestCase(null, null, ExpectedResult = null)]
		[TestCase(null, "Foo", ExpectedResult = "Foo")]
		[TestCase("Foo", null, ExpectedResult = "Foo")]
		[TestCase("Foo", "Bar", ExpectedResult = "Foo")]
		public String Property__CustomHost__environment_overrides_local(String environment, String local)
		{
			_localConfig.service.host = local;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_HOST")).Returns(environment);

			return _defaultConfig.CollectorHost;
		}

		[TestCase(null, null, null, null, ExpectedResult = null)]
		[TestCase(null, null, null, "foo", ExpectedResult = "foo")]
		[TestCase(null, null, "foo", null, ExpectedResult = "foo")]
		[TestCase(null, "foo", null, null, ExpectedResult = "foo")]
		[TestCase(null, null, "foo", "bar", ExpectedResult = "foo")]
		[TestCase(null, "foo", null, "bar", ExpectedResult = "foo")]
		[TestCase(null, "foo", "bar", null, ExpectedResult = "foo")]
		[TestCase(null, "foo", "bar", "baz", ExpectedResult = "foo")]
		[TestCase("foo", null, null, null, ExpectedResult = "foo")]
		[TestCase("foo", null, null, "foo", ExpectedResult = "foo")]
		[TestCase("foo", null, "foo", null, ExpectedResult = "foo")]
		[TestCase("foo", "foo", null, null, ExpectedResult = "foo")]
		[TestCase("foo", null, "foo", "bar", ExpectedResult = "foo")]
		[TestCase("foo", "foo", null, "bar", ExpectedResult = "foo")]
		[TestCase("foo", "foo", "bar", null, ExpectedResult = "foo")]
		[TestCase("foo", "foo", "bar", "baz", ExpectedResult = "foo")]
		public String Property__LicenseKey__environment_overrides_local(String appSettingEnvironmentName, String newEnvironmentName, String legacyEnvironmentName, String local)
		{
			_localConfig.service.licenseKey = local;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns(newEnvironmentName);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns(legacyEnvironmentName);
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey")).Returns(appSettingEnvironmentName);

			return _defaultConfig.AgentLicenseKey;
		}

		[Test]
		public void Property__UrlRegexRules__PullsValueFromServerConfiguration()
		{
			_serverConfig.UrlRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = "banana",
					EachSegment = true,
					EvaluationOrder = 1,
					Ignore = true,
					ReplaceAll = true,
					TerminateChain = true
				},
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "pie"
				}
			};

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.UrlRegexRules.Count()),

				// Rule 1
				() => Assert.AreEqual("apple", _defaultConfig.UrlRegexRules.ElementAt(0).MatchExpression),
				() => Assert.AreEqual("banana", _defaultConfig.UrlRegexRules.ElementAt(0).Replacement),
				() => Assert.AreEqual(true, _defaultConfig.UrlRegexRules.ElementAt(0).EachSegment),
				() => Assert.AreEqual(1, _defaultConfig.UrlRegexRules.ElementAt(0).EvaluationOrder),
				() => Assert.AreEqual(true, _defaultConfig.UrlRegexRules.ElementAt(0).Ignore),
				() => Assert.AreEqual(true, _defaultConfig.UrlRegexRules.ElementAt(0).ReplaceAll),
				() => Assert.AreEqual(true, _defaultConfig.UrlRegexRules.ElementAt(0).TerminateChain),

				// Rule 2
				() => Assert.AreEqual("pie", _defaultConfig.UrlRegexRules.ElementAt(1).MatchExpression),
				() => Assert.AreEqual(null, _defaultConfig.UrlRegexRules.ElementAt(1).Replacement),
				() => Assert.AreEqual(false, _defaultConfig.UrlRegexRules.ElementAt(1).EachSegment),
				() => Assert.AreEqual(0, _defaultConfig.UrlRegexRules.ElementAt(1).EvaluationOrder),
				() => Assert.AreEqual(false, _defaultConfig.UrlRegexRules.ElementAt(1).Ignore),
				() => Assert.AreEqual(false, _defaultConfig.UrlRegexRules.ElementAt(1).ReplaceAll),
				() => Assert.AreEqual(false, _defaultConfig.UrlRegexRules.ElementAt(1).TerminateChain)
				);
		}

		[Test]
		[TestCase("\\1", "$1")]
		[TestCase("\\12", "$12")]
		[TestCase("\\1\\2", "$1$2")]
		[TestCase("\\2banana\\1", "$2banana$1")]
		[TestCase("\\s", "\\s")]
		public void Property__UrlRegexRules__UpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] String input, [NotNull] String expectedOutput)
		{
			_serverConfig.UrlRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = input,
				},
			};

			Assert.AreEqual(expectedOutput, _defaultConfig.UrlRegexRules.ElementAt(0).Replacement);
		}

		[Test]
		public void Property__MetricNameRegexRules__PullsValueFromServerConfiguration()
		{
			_serverConfig.MetricNameRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = "banana",
					EachSegment = true,
					EvaluationOrder = 1,
					Ignore = true,
					ReplaceAll = true,
					TerminateChain = true
				},
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "pie"
				}
			};

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.MetricNameRegexRules.Count()),

				// Rule 1
				() => Assert.AreEqual("apple", _defaultConfig.MetricNameRegexRules.ElementAt(0).MatchExpression),
				() => Assert.AreEqual("banana", _defaultConfig.MetricNameRegexRules.ElementAt(0).Replacement),
				() => Assert.AreEqual(true, _defaultConfig.MetricNameRegexRules.ElementAt(0).EachSegment),
				() => Assert.AreEqual(1, _defaultConfig.MetricNameRegexRules.ElementAt(0).EvaluationOrder),
				() => Assert.AreEqual(true, _defaultConfig.MetricNameRegexRules.ElementAt(0).Ignore),
				() => Assert.AreEqual(true, _defaultConfig.MetricNameRegexRules.ElementAt(0).ReplaceAll),
				() => Assert.AreEqual(true, _defaultConfig.MetricNameRegexRules.ElementAt(0).TerminateChain),

				// Rule 2
				() => Assert.AreEqual("pie", _defaultConfig.MetricNameRegexRules.ElementAt(1).MatchExpression),
				() => Assert.AreEqual(null, _defaultConfig.MetricNameRegexRules.ElementAt(1).Replacement),
				() => Assert.AreEqual(false, _defaultConfig.MetricNameRegexRules.ElementAt(1).EachSegment),
				() => Assert.AreEqual(0, _defaultConfig.MetricNameRegexRules.ElementAt(1).EvaluationOrder),
				() => Assert.AreEqual(false, _defaultConfig.MetricNameRegexRules.ElementAt(1).Ignore),
				() => Assert.AreEqual(false, _defaultConfig.MetricNameRegexRules.ElementAt(1).ReplaceAll),
				() => Assert.AreEqual(false, _defaultConfig.MetricNameRegexRules.ElementAt(1).TerminateChain)
				);
		}

		[Test]
		[TestCase("\\1", "$1")]
		[TestCase("\\12", "$12")]
		[TestCase("\\1\\2", "$1$2")]
		[TestCase("\\2banana\\1", "$2banana$1")]
		[TestCase("\\s", "\\s")]
		public void Property__MetricNameRegexRules__UpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] String input, [NotNull] String expectedOutput)
		{
			_serverConfig.MetricNameRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = input,
				},
			};

			Assert.AreEqual(expectedOutput, _defaultConfig.MetricNameRegexRules.ElementAt(0).Replacement);
		}

		[Test]
		public void Property__TransactionNameRegexRules__PullsValueFromServerConfiguration()
		{
			_serverConfig.TransactionNameRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = "banana",
					EachSegment = true,
					EvaluationOrder = 1,
					Ignore = true,
					ReplaceAll = true,
					TerminateChain = true
				},
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "pie"
				}
			};

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.TransactionNameRegexRules.Count()),

				// Rule 1
				() => Assert.AreEqual("apple", _defaultConfig.TransactionNameRegexRules.ElementAt(0).MatchExpression),
				() => Assert.AreEqual("banana", _defaultConfig.TransactionNameRegexRules.ElementAt(0).Replacement),
				() => Assert.AreEqual(true, _defaultConfig.TransactionNameRegexRules.ElementAt(0).EachSegment),
				() => Assert.AreEqual(1, _defaultConfig.TransactionNameRegexRules.ElementAt(0).EvaluationOrder),
				() => Assert.AreEqual(true, _defaultConfig.TransactionNameRegexRules.ElementAt(0).Ignore),
				() => Assert.AreEqual(true, _defaultConfig.TransactionNameRegexRules.ElementAt(0).ReplaceAll),
				() => Assert.AreEqual(true, _defaultConfig.TransactionNameRegexRules.ElementAt(0).TerminateChain),

				// Rule 2
				() => Assert.AreEqual("pie", _defaultConfig.TransactionNameRegexRules.ElementAt(1).MatchExpression),
				() => Assert.AreEqual(null, _defaultConfig.TransactionNameRegexRules.ElementAt(1).Replacement),
				() => Assert.AreEqual(false, _defaultConfig.TransactionNameRegexRules.ElementAt(1).EachSegment),
				() => Assert.AreEqual(0, _defaultConfig.TransactionNameRegexRules.ElementAt(1).EvaluationOrder),
				() => Assert.AreEqual(false, _defaultConfig.TransactionNameRegexRules.ElementAt(1).Ignore),
				() => Assert.AreEqual(false, _defaultConfig.TransactionNameRegexRules.ElementAt(1).ReplaceAll),
				() => Assert.AreEqual(false, _defaultConfig.TransactionNameRegexRules.ElementAt(1).TerminateChain)
				);
		}

		[Test]
		[TestCase("\\1", "$1")]
		[TestCase("\\12", "$12")]
		[TestCase("\\1\\2", "$1$2")]
		[TestCase("\\2banana\\1", "$2banana$1")]
		[TestCase("\\s", "\\s")]
		public void Property__TransactionNameRegexRules__UpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] String input, [NotNull] String expectedOutput)
		{
			_serverConfig.TransactionNameRegexRules = new List<ServerConfiguration.RegexRule>
			{
				new ServerConfiguration.RegexRule
				{
					MatchExpression = "apple",
					Replacement = input,
				},
			};

			Assert.AreEqual(expectedOutput, _defaultConfig.TransactionNameRegexRules.ElementAt(0).Replacement);
		}

		[Test]
		public void Property__WebTransactionsApdex__PullsValueFromServerConfiguration()
		{
			_serverConfig.WebTransactionsApdex = new Dictionary<String, Double>
			{
				{"apple", 0.2},
				{"banana", 0.1}
			};

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.WebTransactionsApdex.Count),
				() => Assert.IsTrue(_defaultConfig.WebTransactionsApdex.ContainsKey("apple")),
				() => Assert.AreEqual(0.2, _defaultConfig.WebTransactionsApdex["apple"]),
				() => Assert.IsTrue(_defaultConfig.WebTransactionsApdex.ContainsKey("banana")),
				() => Assert.AreEqual(0.1, _defaultConfig.WebTransactionsApdex["banana"])
				);
		}

		[Test]
		public void Property__TransactionNameWhitelistRules__PullsValueFromServerConfiguration()
		{
			_serverConfig.TransactionNameWhitelistRules = new List<ServerConfiguration.WhitelistRule>
			{
				new ServerConfiguration.WhitelistRule
				{
					Prefix = "apple/banana",
					Terms = new List<String> {"pie", "cake"}
				},
				new ServerConfiguration.WhitelistRule
				{
					Prefix = "mango/peach/",
					Terms = new List<String>()
				}
			};

			// ReSharper disable AssignNullToNotNullAttribute
			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.TransactionNameWhitelistRules.Count()),

				// Rule 1
				() => Assert.IsTrue(_defaultConfig.TransactionNameWhitelistRules.ContainsKey("apple/banana")),
				() => Assert.NotNull(_defaultConfig.TransactionNameWhitelistRules["apple/banana"]),
				() => Assert.AreEqual(2, _defaultConfig.TransactionNameWhitelistRules["apple/banana"].Count()),
				() => Assert.AreEqual("pie", _defaultConfig.TransactionNameWhitelistRules["apple/banana"].ElementAt(0)),
				() => Assert.AreEqual("cake", _defaultConfig.TransactionNameWhitelistRules["apple/banana"].ElementAt(1)),

				// Rule 2
				() => Assert.IsTrue(_defaultConfig.TransactionNameWhitelistRules.ContainsKey("mango/peach")),
				() => Assert.NotNull(_defaultConfig.TransactionNameWhitelistRules["mango/peach"]),
				() => Assert.AreEqual(0, _defaultConfig.TransactionNameWhitelistRules["mango/peach"].Count())
				);
			// ReSharper restore AssignNullToNotNullAttribute
		}

		#region ApplicationNames

		[Test]
		public void Property__ApplicationNames__ThrowsIfNoAppNameFound()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

			_localConfig.application.name = new List<string>();

			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns((string)null);

			Assert.Throws<Exception>(() => _defaultConfig.ApplicationNames.Any());
		}

		[Test]
		public void Property__ApplicationNames__PullsNamesFrom_RuntimeConfig()
		{
			_runTimeConfig.ApplicationNames = new List<String> { "MyAppName1", "MyAppName2" };
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((String)null);
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((String)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns((String)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns((String)null);
			_localConfig.application.name = new List<String>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsSingleNameFrom_AppSettings()
		{
			_runTimeConfig.ApplicationNames = new List<String>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns("MyAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
			_localConfig.application.name = new List<String>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsMultipleNamesFrom_AppSettings()
		{
			_runTimeConfig.ApplicationNames = new List<String>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns("MyAppName1,MyAppName2");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
			_localConfig.application.name = new List<String>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsSingleNameFrom_IISExpressSitenameEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<String>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((String)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
			_localConfig.application.name = new List<String>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsMultipleNamesFrom_IISExpressSitenameEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<String>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((String)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName1,MyAppName2");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
			_localConfig.application.name = new List<String>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsSingleNameFrom_RoleNameEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("MyAppName");
			_localConfig.application.name = new List<string>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsMultipleNamesFrom_RoleNameEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("MyAppName1,MyAppName2");
			_localConfig.application.name = new List<string>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsNamesFrom_NewRelicConfig()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

			_localConfig.application.name = new List<string> {"MyAppName1", "MyAppName2"};
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("OtherAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsSingleNameFrom_AppPoolIdEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((string)null);

			_localConfig.application.name = new List<string>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("MyAppName");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsMultipleNamesFrom_AppPoolIdEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

			_localConfig.application.name = new List<string>();
			Mock.Arrange(() => _environment.GetEnvironmentVariable("APP_POOL_ID")).Returns("MyAppName1,MyAppName2");
			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(2, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName1", _defaultConfig.ApplicationNames.FirstOrDefault()),
				() => Assert.AreEqual("MyAppName2", _defaultConfig.ApplicationNames.ElementAtOrDefault(1))
				);
		}

		[Test]
		public void Property__ApplicationNames__PullsNameFrom_ProcessId_IfAppDomainAppVirtualPathIsNull()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);
			
			_localConfig.application.name = new List<String>();

			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns<string>(null);
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("MyAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}


		#endregion ApplicationNames


		[Test]
		public void Property__AutostartAgent__PullsFromLocalConfig()
		{
			_localConfig.service.autoStart = false;
			Assert.IsFalse(_defaultConfig.AutoStartAgent);

			_localConfig.service.autoStart = true;
			Assert.IsTrue(_defaultConfig.AutoStartAgent);
		}

		#region CrossApplicationTracingEnabled

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsTrueIfAllCatFlagsEnabledAndCrossProcessIdIsNotNull()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsTrueIfCrossApplicationTracerIsMissingButAllOtherFlagsEnabled()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer = null;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsFalseIfCrossApplicationTracingEnabledIsFalse()
		{
			_localConfig.crossApplicationTracingEnabled = false;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsFalseIfRpmConfigCrossApplicationTracerEnabledIsFalse()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = false;
			_serverConfig.CatId = "123#456";

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsFalseIfCatIdIsNull()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = null;

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsTrueWithNewServerConfig()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig = new ServerConfiguration();
			_serverConfig.CatId = "123#456";
			_defaultConfig = new DefaultConfigurationTest(_environment, _localConfig, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void Property__CrossApplicationTracingEnabled__IsFalseWithGetDefaultServerConfig()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig = ServerConfiguration.GetDefault();
			_serverConfig.CatId = "123#456";
			_defaultConfig = new DefaultConfigurationTest(_environment, _localConfig, _serverConfig, _runTimeConfig, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		#endregion CrossApplicationTracingEnabled

		#region Utilization

		[Test]
		public void Property_UtilizationDetectAws_IsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectAws);
		}

		[Test]
		public void Property_UtilizationDetectAzure_IsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectAzure);
		}

		[Test]
		public void Property_UtilizationDetectPcf_IsTrueByDefualt()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectPcf);
		}

		[Test]
		public void Property_UtilizationDetectGcp_IsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectGcp);
		}

		[Test]
		public void Property_UtilizationDetectDocker_IsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectDocker);
		}

		[Test]
		public void Property_UtilizationDetectAws_IsSetToFalse()
		{
			_localConfig.utilization.detectAws = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectAws);
		}

		[Test]
		public void Property_UtilizationDetectAzure_IsSetToFalse()
		{
			_localConfig.utilization.detectAzure = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectAzure);
		}

		[Test]
		public void Property_UtilizationDetectPcf_IsSetToFalse()
		{
			_localConfig.utilization.detectPcf = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectPcf);
		}

		[Test]
		public void Property_UtilizationDetectGcp_IsSetToFalse()
		{
			_localConfig.utilization.detectGcp = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectGcp);
		}

		[Test]
		public void Property_UtilizationDetectDocker_IsSetToFalse()
		{
			_localConfig.utilization.detectDocker = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectDocker);
		}

		#endregion
	}
}
