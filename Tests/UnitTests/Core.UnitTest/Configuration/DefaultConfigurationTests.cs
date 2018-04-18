using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using JetBrains.Annotations;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

// ReSharper disable InconsistentNaming
// ReSharper disable CheckNamespace
namespace NewRelic.Agent.Core.Configuration.UnitTest
{
	internal class TestableDefaultConfiguration : DefaultConfiguration
	{
		public TestableDefaultConfiguration([NotNull] IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, [NotNull] IProcessStatic processStatic, [NotNull] IHttpRuntimeStatic httpRuntimeStatic, [NotNull] IConfigurationManagerStatic configurationManagerStatic) : base(environment, localConfig, serverConfig, runTimeConfiguration, securityPoliciesConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic) { }
	}

	[TestFixture, Category("Configuration")]
	public class DefaultConfigurationTests
	{
		private IEnvironment _environment;
		private IProcessStatic _processStatic;
		private IHttpRuntimeStatic _httpRuntimeStatic;
		private IConfigurationManagerStatic _configurationManagerStatic;
		private configuration _localConfig;
		private ServerConfiguration _serverConfig;
		private RunTimeConfiguration _runTimeConfig;
		private SecurityPoliciesConfiguration _securityPoliciesConfiguration;
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
			_securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
			_defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);
		}

		[Test]
		public void AgentEnabledShouldPassThroughToLocalConfig()
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
		public bool TransactionEventsCanBeDisbledByServer(bool? server, bool? legacyLocal, bool? local)
		{
			_localConfig.transactionEvents.enabled = local ?? default(bool);
			_localConfig.transactionEvents.enabledSpecified = local.HasValue;

			_localConfig.analyticsEvents.enabled = legacyLocal ?? default(bool);
			_localConfig.analyticsEvents.enabledSpecified = legacyLocal.HasValue;

			_serverConfig.AnalyticsEventCollectionEnabled = server;

			return _defaultConfig.TransactionEventsEnabled;
		}

		[Test]
		public void EveryConfigShouldGetNewVersionNumber()
		{
			var newConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.AreEqual(_defaultConfig.ConfigurationVersion, newConfig.ConfigurationVersion - 1);
		}

		[Test]
		public void WhenConfigsAreDefaultThenTransactionEventsAreEnabled()
		{
			Assert.IsTrue(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void WhenConfigsAreDefaultThenPutForDataSendIsDisabled()
		{
			Assert.IsFalse(_defaultConfig.PutForDataSend);
		}

		[Test]
		public void WhenConfigsAreDefaultThenInstanceReportingEnabledIsDisabled()
		{
			Assert.IsTrue(_defaultConfig.InstanceReportingEnabled);
		}

		[Test]
		public void WhenConfigsAreDefaultThenDatabaseNameReportingEnabledIsDisabled()
		{
			Assert.IsTrue(_defaultConfig.DatabaseNameReportingEnabled);
		}

		[Test]
		public void CompressedContentEncodingShouldBeDeflateWhenConfigsAreDefault()
		{
			Assert.AreEqual("deflate", _defaultConfig.CompressedContentEncoding);
		}

		[Test]
		public void WhenTransactionEventsAreEnabledInLocalConfigAndDoNotExistInServerConfigThenTransactionEventsAreEnabled()
		{
			_localConfig.transactionEvents.enabled = true;
			Assert.IsTrue(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void WhenTransactionEventsAreDisabledInLocalConfigAndDoNotExistInServerConfigThenTransactionEventsAreDisabled()
		{
			_localConfig.transactionEvents.enabled = false;
			Assert.IsFalse(_defaultConfig.TransactionEventsEnabled);
		}

		[Test]
		public void TransactionEventsMaxSamplesPerMinuteIsCappedAt10000()
		{

			Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaxSamplesPerMinute);

			_localConfig.transactionEvents.maximumSamplesPerMinute = 10001;
			Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaxSamplesPerMinute);

			_localConfig.transactionEvents.maximumSamplesPerMinute = 9999;
			Assert.AreEqual(9999, _defaultConfig.TransactionEventsMaxSamplesPerMinute);
		}

		[Test]
		public void TransactionEventsMaxSamplesStoredPassesThroughToLocalConfig()
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
		public bool ErrorCollectorEnabledWithRpmCollectorEnabledServerOverrides(bool local, bool? server, bool? rpmConfigServer)
		{
			_localConfig.errorCollector.enabled = local;
			_serverConfig.ErrorCollectionEnabled = server;
			_serverConfig.RpmConfig.ErrorCollectorEnabled = rpmConfigServer;

			return _defaultConfig.ErrorCollectorEnabled;
		}

		[Test]
		public void ErrorsMaximumPerPeriodReturnsStatic20()
		{
			Assert.AreEqual(20, _defaultConfig.ErrorsMaximumPerPeriod);
		}

		[Test]
		public void SqlTracesPerPeriodReturnsStatic10()
		{
			Assert.AreEqual(10, _defaultConfig.SqlTracesPerPeriod);
		}

		[Test]
		public void SlowSqlServerOverridesWhenSet()
		{
			_serverConfig.RpmConfig.SlowSqlEnabled = true;
			_localConfig.slowSql.enabled = false;

			Assert.AreEqual(true, _defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void SlowSqlServerOverridesWhenLocalIsDefault()
		{
			_serverConfig.RpmConfig.SlowSqlEnabled = false;

			Assert.AreEqual(false, _defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void SlowSqlDefaultIsTrue()
		{
			Assert.IsTrue(_defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void SlowSqlLocalConfigSetToFalse()
		{
			_localConfig.slowSql.enabled = false;
			Assert.IsFalse(_defaultConfig.SlowSqlEnabled);
		}

		[Test]
		public void WhenStackTraceMaximumFramesIsSet()
		{
			_localConfig.maxStackTraceLines = 100;
			Assert.AreEqual(100, _defaultConfig.StackTraceMaximumFrames);
		}

		[TestCase(null, ExpectedResult = 80)]
		[TestCase(100, ExpectedResult = 100)]
		public int StackTraceMaximumFramesSetFromLocal(int? maxFrames)
		{
			var value = maxFrames ?? 80;
			_localConfig.maxStackTraceLines = value;
			return _defaultConfig.StackTraceMaximumFrames;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool InstrumentationLoggingEnabledSetFromLocal(bool local)
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
		public bool TransactionTracerEnabledWithRpmCollectorEnabledServerOverrides(bool local, bool? server, bool? rpmConfigServer)
		{
			_localConfig.transactionTracer.enabled = local;
			_serverConfig.TraceCollectionEnabled = server;
			_serverConfig.RpmConfig.TransactionTracerEnabled = rpmConfigServer;

			return _defaultConfig.TransactionTracerEnabled;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool TransactionTracerCaptureAttributesSetFromLocal(bool local)
		{
			_localConfig.transactionTracer.captureAttributes = local;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool Property_DataTransmissionPutForDataSend_set_from_local(bool local)
		{
			_localConfig.dataTransmission.putForDataSend = local;

			return _defaultConfig.PutForDataSend;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool DatastoreTracerInstanceReportingEnabledSetFromLocal(bool local)
		{
			_localConfig.datastoreTracer.instanceReporting.enabled = local;

			return _defaultConfig.InstanceReportingEnabled;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool DatastoreTracerDatabaseNameReportingEnabledSetFromLocal(bool local)
		{
			_localConfig.datastoreTracer.databaseNameReporting.enabled = local;

			return _defaultConfig.DatabaseNameReportingEnabled;
		}

		[TestCase(configurationDataTransmissionCompressedContentEncoding.deflate, ExpectedResult = "deflate")]
		[TestCase(configurationDataTransmissionCompressedContentEncoding.gzip, ExpectedResult = "gzip")]
		public string CompressedContentEncodingShouldSetFromLocalConfiguration(configurationDataTransmissionCompressedContentEncoding local)
		{
			_localConfig.dataTransmission.compressedContentEncoding = local;
			return _defaultConfig.CompressedContentEncoding;
		}


		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool ErrorCollectorCatpureEventsSetFromLocal(bool local)
		{
			_localConfig.errorCollector.captureEvents = local;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public bool ErrorCollectorCatpureEventsSetFromServer(bool local, bool server)
		{
			_localConfig.errorCollector.captureEvents = local;
			_serverConfig.RpmConfig.ErrorCollectorCaptureEvents = server;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		public bool ErrorCollectorCaptureEventsConditionalOverrideFromServer(bool local, bool server)
		{
			_localConfig.errorCollector.captureEvents = local;
			_serverConfig.RpmConfig.CollectErrorEvents = server;
			return _defaultConfig.ErrorCollectorCaptureEvents;
		}

		[TestCase(50, ExpectedResult = 50)]
		public uint ErrorCollectorMaxNumberEventSamplesSetFromLocal(int local)
		{
			_localConfig.errorCollector.maxEventSamplesStored = local;
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(50, 75, ExpectedResult = 75)]
		public uint ErrorCollectorMaxNumberEventSamplesSetFromServer(int local, int server)
		{
			_localConfig.errorCollector.maxEventSamplesStored = local;
			_serverConfig.RpmConfig.ErrorCollectorMaxEventSamplesStored = (uint?)server;
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(ExpectedResult = 100)]
		public uint ErrorCollectorMaxNumberEventSamplesDefaultFromLocal()
		{
			return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool BrowserMonitoringCaptureAttributesSetFromLocal(bool local)
		{
			_localConfig.browserMonitoring.captureAttributes = local;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[TestCase(null, ExpectedResult = 3)]
		[TestCase(42, ExpectedResult = 42)]
		public int InstrumentationLevelServerOverridesDefault(int? server)
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
		public bool SqlExplainPlansEnabledServerOverridesLocal(bool local, bool? server)
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
		public double ExplainPlanThresholdSetFromServerOverridesLocal(double local, double? server)
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
		public void SqlStatementsPerTransactionAlwaysReturns500()
		{
			Assert.AreEqual(500, _defaultConfig.SqlStatementsPerTransaction);
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, null, ExpectedResult = false)]
		public bool TransactionEventsEnabledSetFromLocalServerCanDisable(bool local, bool? server)
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
		public bool CaptureParametersSetFromLocalServerOverrides(bool local, bool? server)
		{
			_serverConfig.RpmConfig.CaptureParametersEnabled = server;
			_localConfig.transactionEvents.enabled = local;

			return _defaultConfig.CaptureRequestParameters;
		}

		[TestCase(new[] {"local"}, new[] {"server"}, "request.parameters.server")]
		[TestCase(new[] {"local"}, null, "request.parameters.local")]
		public void RequestParametersToIgnoreSetFromLocalServerOverrides(string[] local, string[] server, string expected)
		{
			_serverConfig.RpmConfig.ParametersToIgnore = server;
			_localConfig.requestParameters.ignore = new List<string>(local);

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains(expected));
		}

		[TestCase(false, configurationTransactionTracerRecordSql.obfuscated, null, ExpectedResult = "obfuscated")]
		[TestCase(false, configurationTransactionTracerRecordSql.off, null, ExpectedResult = "off")]
		[TestCase(false, configurationTransactionTracerRecordSql.raw, null, ExpectedResult = "raw")]
		[TestCase(false, configurationTransactionTracerRecordSql.obfuscated, "off", ExpectedResult = "off")] 
		[TestCase(false, configurationTransactionTracerRecordSql.off, "obfuscated", ExpectedResult = "obfuscated")] 
		[TestCase(false, configurationTransactionTracerRecordSql.raw, "off", ExpectedResult = "off")] 
		[TestCase(true, configurationTransactionTracerRecordSql.off, null, ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, null, ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.raw, null, ExpectedResult = "obfuscated")]
		[TestCase(true, configurationTransactionTracerRecordSql.off, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.raw, "off", ExpectedResult = "off")]
		[TestCase(true, configurationTransactionTracerRecordSql.obfuscated, "raw", ExpectedResult = "obfuscated")]
		public string TransactionTracerRecordSqlSetFromLocalAndServerHighSecurityOverridesServerOverrides(bool highSecurity, configurationTransactionTracerRecordSql local, string server)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.transactionTracer.recordSql = local;
			_serverConfig.RpmConfig.TransactionTracerRecordSql = server;

			return _defaultConfig.TransactionTracerRecordSql;
		}

		[TestCase(configurationTransactionTracerRecordSql.raw, "raw", true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.raw, "raw", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.raw, "obfuscated", true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.raw, "obfuscated", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.raw, null, true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.raw, null, false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, "raw", true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, "raw", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, "obfuscated", true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, "obfuscated", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, null, true, ExpectedResult = "obfuscated")]
		[TestCase(configurationTransactionTracerRecordSql.obfuscated, null, false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, "raw", true, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, "raw", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, "obfuscated", true, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, "obfuscated", false, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, null, true, ExpectedResult = "off")]
		[TestCase(configurationTransactionTracerRecordSql.off, null, false, ExpectedResult = "off")]
		public string RecordSqlMostSecureWinsNeverRawWithSecurityPolicies(configurationTransactionTracerRecordSql local, string server, bool securityPolicyEnabled)
		{
			SetupNewConfigsWithSecurityPolicy("record_sql", securityPolicyEnabled);
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
		public bool HighSecuritySetFromLocalOverridesServer(bool local, bool? server)
		{
			_localConfig.highSecurity.enabled = local;
			_serverConfig.HighSecurityEnabled = server;

			return _defaultConfig.HighSecurityModeEnabled;
		}

		[TestCase(true, true, ExpectedResult = false)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public bool CustomInstrumentationEditorHighSecurityOverrides(bool highSecurity, bool customInstrumentationEditor)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.customInstrumentationEditor.enabled = customInstrumentationEditor;

			return _defaultConfig.CustomInstrumentationEditorEnabled;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		public bool CustomInstrumentationEditorMostSecureWinsWithSecurityPolicies(bool localEnabled, bool securityPolicyEnabled)
		{
			SetupNewConfigsWithSecurityPolicy("custom_instrumentation_editor", securityPolicyEnabled);

			_localConfig.customInstrumentationEditor.enabled = localEnabled;

			return _defaultConfig.CustomInstrumentationEditorEnabled;
		}

		private void SetupNewConfigsWithSecurityPolicy(string securityPolicyName, bool securityPolicyEnabled)
		{
			var securityPolicies = new Dictionary<string, SecurityPolicyState> { { securityPolicyName, new SecurityPolicyState(securityPolicyEnabled, false) } };
			_securityPoliciesConfiguration = new SecurityPoliciesConfiguration(securityPolicies);
			_defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		public bool StripExceptionMessagesHighSecurityOverrides(bool highSecurity, bool stripErrorMessages)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.stripExceptionMessages.enabled = stripErrorMessages;

			return _defaultConfig.StripExceptionMessages;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = true)]
		public bool StripExceptionMessagesMostSecureWinsWithSecurityPolicies(bool localEnabled, bool securityPolicyEnabled)
		{
			SetupNewConfigsWithSecurityPolicy("allow_raw_exception_messages", securityPolicyEnabled);
			_localConfig.stripExceptionMessages.enabled = localEnabled;

			return _defaultConfig.StripExceptionMessages;
		}

		[Test]
		public void UseSslOverriddenByLocalHighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.browserMonitoring.sslForHttp = false;

			Assert.IsTrue(_defaultConfig.BrowserMonitoringUseSsl);
		}

		[TestCase(true, true, ExpectedResult = false)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		public bool LegacyCaptureCustomParametersOverriddenByLocalHighSecurity(bool highSecurityEnabled, bool localEnabled)
		{
			_localConfig.highSecurity.enabled = highSecurityEnabled;
			_localConfig.parameterGroups.customParameters.enabled = localEnabled;

			return _defaultConfig.CaptureCustomParameters;
		}

		[TestCase(true, true, ExpectedResult = false)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		public bool CaptureCustomParametersOverriddenByLocalHighSecurity(bool highSecurityEnabled, bool localEnabled)
		{
			_localConfig.highSecurity.enabled = highSecurityEnabled;
			_localConfig.customParameters.enabled = localEnabled;

			return _defaultConfig.CaptureCustomParameters;
		}

		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(true, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		[TestCase(false, false, ExpectedResult = false)]
		public bool CaptureCustomParametersMostSecureWinsWithSecurityPolicies(bool localEnabled, bool securityPolicyEnabled)
		{
			SetupNewConfigsWithSecurityPolicy("custom_parameters", securityPolicyEnabled);
			_localConfig.customParameters.enabled = localEnabled;

			return _defaultConfig.CaptureCustomParameters;
		}

		[Test]
		public void CaptureRequestParametersOverriddenByLocalHighSecurity()
		{
			_localConfig.highSecurity.enabled = true;
			_localConfig.requestParameters.enabled = true;

			Assert.IsFalse(_defaultConfig.CaptureRequestParameters);
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
		public double TransactionTraceThresholdSetFromServerOverridesLocal(string local, object server, double apdexT)
		{
			_serverConfig.ApdexT = apdexT;
			_serverConfig.RpmConfig.TransactionTracerThreshold = server;
			_localConfig.transactionTracer.transactionThreshold = local;

			return _defaultConfig.TransactionTraceThreshold.TotalMilliseconds;
		}

		[Test]
		public void WhenTransactionTraceThresholdSetToApdexfThenEqualsApdextTimes4()
		{
			_serverConfig.ApdexT = 42;
			_localConfig.transactionTracer.transactionThreshold = "apdex_f";

			Assert.AreEqual(42*4, _defaultConfig.TransactionTraceThreshold.TotalSeconds);
		}

		[Test]
		public void CaptureCustomParametersSetFromLocalDefaultsToTrue()
		{
			Assert.IsTrue(_defaultConfig.CaptureCustomParameters);
		}

		[TestCase(false, true, true, false, false, ExpectedResult = true)]
		[TestCase(false, true, false, false, false, ExpectedResult = false)]
		[TestCase(false, false, true, true, true, ExpectedResult = true)]
		[TestCase(false, false, true, true, false, ExpectedResult = false)]
		[TestCase(false, true, true, true, false, ExpectedResult = false)]
		[TestCase(true, false, true, true, true, ExpectedResult = false)]
		[TestCase(true, true, true, false, true, ExpectedResult = false)]
		[TestCase(true, true, true, true, true, ExpectedResult = false)]
		public bool CaptureCustomParametersHsmDeprecatedAndNew(bool highSecurity, bool deprecatedCustomParametersSpecified, bool deprecatedCustomParametersEnabled, bool customParametersSpecified, bool customParametersEnabled)
		{
			_localConfig.highSecurity.enabled = highSecurity;

			if (deprecatedCustomParametersSpecified)
			{
				_localConfig.parameterGroups.customParameters.enabledSpecified = deprecatedCustomParametersSpecified;
				_localConfig.parameterGroups.customParameters.enabled = deprecatedCustomParametersEnabled;
			} 
			
			if ( customParametersSpecified )
			{
				_localConfig.customParameters.enabledSpecified = customParametersSpecified;
				_localConfig.customParameters.enabled = customParametersEnabled;
			}

			return _defaultConfig.CaptureCustomParameters;
		}

		[Test]
		public void CustomParametersToIgnoreSetFromLocal()
		{
			_localConfig.parameterGroups.customParameters.ignore = new List<string>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("local"));
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool CaptureIdentityParametersSetFromLocal(bool isEnabled)
		{
			_localConfig.parameterGroups.identityParameters.enabled = isEnabled;
			return _defaultConfig.CaptureErrorCollectorAttributesIncludes.Contains("identity.*");
		}

		[Test]
		public void CaptureIdentityParametersSetFromLocalDefaultsToFalse()
		{
			Assert.IsTrue(_defaultConfig.CaptureAttributesDefaultExcludes.Contains("identity.*"));
		}

		[Test]
		public void IdentityParametersToIgnoreSetFromLocal()
		{
			_localConfig.parameterGroups.identityParameters.ignore = new List<string>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("identity.local"));
		}

		[TestCase(true, ExpectedResult = false)]
		[TestCase(false, ExpectedResult = true)]
		public bool CaptureResponseHeaderParametersSetFromLocal(bool isEnabled)
		{
			_localConfig.parameterGroups.responseHeaderParameters.enabled = isEnabled;
			return _defaultConfig.CaptureAttributesExcludes.Contains("response.headers.*");
		}

		[Test]
		public void CaptureResponseHeaderParametersSetFromLocalDefaultsToTrue()
		{
			Assert.IsFalse(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.*"));
		}

		[Test]
		public void ResponseHeaderParametersToIgnoreSetFromLocal()
		{
			_localConfig.parameterGroups.responseHeaderParameters.ignore = new List<string>() {"local"};

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.local"));
		}

		[TestCase(new[] {"local"}, new[] {"server"}, ExpectedResult = "server")]
		[TestCase(new[] {"local"}, null, ExpectedResult = "local")]
		public string ExceptionsToIgnoreSetFromLocalAndServerOverrides(string[] local, string[] server)
		{
			_serverConfig.RpmConfig.ErrorCollectorErrorsToIgnore = server;
			_localConfig.errorCollector.ignoreErrors.exception = new List<string>(local);

			return _defaultConfig.ExceptionsToIgnore.FirstOrDefault();
		}

		[TestCase("local", "server", ExpectedResult = "server")]
		[TestCase("local", null, ExpectedResult = "local")]
		[TestCase("local", "", ExpectedResult = "")] //If server sends back string.empty then override with that
		public string BrowserMonitoringJavaScriptAgentLoaderTypeSetFromLocalAndServerOverrides(string local, string server)
		{
			_serverConfig.RumSettingsBrowserMonitoringLoader = server;
			_localConfig.browserMonitoring.loader = local;

			return _defaultConfig.BrowserMonitoringJavaScriptAgentLoaderType;
		}

		[TestCase(new float[] {400, 404}, new[] {"500"}, ExpectedResult = "500")]
		[TestCase(new float[] {400, 404}, null, ExpectedResult = "400")]
		public string StatusCodesToIgnoreSetFromLocalAndServerOverrides(float[] local, string[] server)
		{
			_serverConfig.RpmConfig.ErrorCollectorStatusCodesToIgnore = server;
			_localConfig.errorCollector.ignoreStatusCodes.code = new List<float>(local);

			return _defaultConfig.HttpStatusCodesToIgnore.FirstOrDefault();
		}

		[Test]
		public void StaticFieldInstanceIsNotNull()
		{
			Assert.NotNull(DefaultConfiguration.Instance);
		}

		[Test]
		public void StaticRequestPathExclusionListIsNotNull()
		{
			Assert.NotNull(DefaultConfiguration.Instance.RequestPathExclusionList);
		}

		[Test]
		public void RequestPathExclusionListIsEmptyIfNoRequestPathsInList()
		{
			Assert.IsEmpty(DefaultConfiguration.Instance.RequestPathExclusionList);
		}

		[Test]
		public void RequestPathExclusionListContainsOneEntry()
		{
			var path = new configurationBrowserMonitoringPath();
			path.regex = "one";

			_localConfig.browserMonitoring.requestPathsExcluded.Add(path);

			Assert.AreEqual(1, _defaultConfig.RequestPathExclusionList.Count());
		}

		[Test]
		public void RequestPathExclusionListContainsTwoEntries()
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
		public void RequestPathExclusionListBadRegex()
		{
			var path = new configurationBrowserMonitoringPath();
			path.regex = ".*(?<!\\)\\(?!\\).*";

			_localConfig.browserMonitoring.requestPathsExcluded.Add(path);

			Assert.AreEqual(0, _defaultConfig.RequestPathExclusionList.Count());
		}

		[Test]
		public void BrowserMonitoringJavaScriptAgentLoaderTypeSetToDefaultRum()
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
		public int TransactionTracerStackThresholdServerOverridesLocal(int local, double? server)
		{
			_localConfig.transactionTracer.stackTraceThreshold = local;
			_serverConfig.RpmConfig.TransactionTracerStackThreshold = server;

			return _defaultConfig.TransactionTracerStackThreshold.Milliseconds;
		}

		[Test]
		public void ThreadProfilingIgnoreMethodFromXmlDecodesIntoListOfStrings()
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

			_defaultConfig = new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsTrue(_defaultConfig.ThreadProfilingIgnoreMethods.Contains("System.Threading.WaitHandle:WaitAny"));
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool BrowserMonitoringOverridesDeprecatedValue(bool propertyEnabled, bool deprecatedEnabled)
		{
			_localConfig.browserMonitoring.captureAttributes = deprecatedEnabled;
			_localConfig.browserMonitoring.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool BrowserMonitoringDeprecatedValueOverridesDefault(bool deprecatedEnabled)
		{
			_localConfig.browserMonitoring.captureAttributesSpecified = false;
			_localConfig.browserMonitoring.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureBrowserMonitoringAttributes;
		}

		[Test]
		public void BrowserMonitoringUsesDefaultWhenNoConfigValues()
		{
			_localConfig.browserMonitoring.captureAttributesSpecified = false;
			_localConfig.browserMonitoring.attributes.enabledSpecified = false;

			Assert.IsFalse(_defaultConfig.CaptureBrowserMonitoringAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool ErrorCollectorOverridesDeprecatedValue(bool propertyEnabled, bool deprecatedEnabled)
		{
			_localConfig.errorCollector.captureAttributes = deprecatedEnabled;
			_localConfig.errorCollector.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureErrorCollectorAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool ErrorCollectorDeprecatedValueOverridesDefault(bool deprecatedEnabled)
		{
			_localConfig.errorCollector.captureAttributesSpecified = false;
			_localConfig.errorCollector.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureErrorCollectorAttributes;
		}

		[Test]
		public void ErrorCollectorUsesDefaultWhenNoConfigValues()
		{
			_localConfig.errorCollector.captureAttributesSpecified = false;
			_localConfig.errorCollector.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureErrorCollectorAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool TransactionTracerOverridesDeprecatedValue(bool propertyEnabled, bool deprecatedEnabled)
		{
			_localConfig.transactionTracer.captureAttributes = deprecatedEnabled;
			_localConfig.transactionTracer.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool TransactionTracerDeprecatedValueOverridesDefault(bool deprecatedEnabled)
		{
			_localConfig.transactionTracer.captureAttributesSpecified = false;
			_localConfig.transactionTracer.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureTransactionTraceAttributes;
		}

		[Test]
		public void TransactionTracerUsesDefaultWhenNoConfigValues()
		{
			_localConfig.transactionTracer.captureAttributesSpecified = false;
			_localConfig.transactionTracer.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureTransactionTraceAttributes);
		}

		[TestCase(true, false, ExpectedResult = true)]
		[TestCase(true, true, ExpectedResult = true)]
		[TestCase(false, false, ExpectedResult = false)]
		[TestCase(false, true, ExpectedResult = false)]
		public bool TransactionEventOverridesDeprecatedValue(bool propertyEnabled, bool deprecatedEnabled)
		{
			_localConfig.analyticsEvents.captureAttributes = deprecatedEnabled;
			_localConfig.transactionEvents.attributes.enabled = propertyEnabled;

			return _defaultConfig.CaptureTransactionEventsAttributes;
		}

		[TestCase(true, ExpectedResult = true)]
		[TestCase(false, ExpectedResult = false)]
		public bool AnalyticsEventDeprecatedValueOverridesDefault(bool deprecatedEnabled)
		{
			_localConfig.analyticsEvents.captureAttributesSpecified = false;
			_localConfig.transactionEvents.attributes.enabled = deprecatedEnabled;

			return _defaultConfig.CaptureTransactionEventsAttributes;
		}

		[Test]
		public void TransactionEventUsesDefaultWhenNoConfigValues()
		{
			_localConfig.analyticsEvents.captureAttributesSpecified = false;
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureTransactionEventsAttributes);
		}

		[Test]
		public void DeprecatedIgnoreIdentityParametersValueBecomesExclude()
		{
			_localConfig.parameterGroups.identityParameters.ignore = new List<string>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("identity.foo"));
		}

		[Test]
		public void DeprecatedIgnoreCustomParametersValueBecomesExclude()
		{
			_localConfig.parameterGroups.customParameters.ignore = new List<string>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("foo"));
		}

		[Test]
		public void DeprecatedIgnoreResponseHeaderParametersValueBecomesExclude()
		{
			_localConfig.parameterGroups.responseHeaderParameters.ignore = new List<string>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.foo"));
		}

		[Test]
		public void DeprecatedIgnoreRequestHeaderParametersValueBecomesExclude()
		{
			_localConfig.parameterGroups.requestHeaderParameters.ignore = new List<string>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("request.headers.foo"));
		}

		[Test]
		public void Property_deprecated_ignore_requestParameters_value_becomes_exclude()
		{
			_localConfig.requestParameters.ignore = new List<string>() {"foo"};
			_localConfig.transactionEvents.attributes.enabledSpecified = false;

			Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("request.parameters.foo"));
		}

		[TestCase(null, null, ExpectedResult = null)]
		[TestCase(null, "Foo", ExpectedResult = "Foo")]
		[TestCase("Foo", null, ExpectedResult = "Foo")]
		[TestCase("Foo", "Bar", ExpectedResult = "Foo")]
		public string LabelsEnvironmentOverridesLocal(string environment, string local)
		{
			_localConfig.labels = local;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LABELS")).Returns(environment);

			return _defaultConfig.Labels;
		}

		[TestCase(null, null, ExpectedResult = null)]
		[TestCase(null, "Foo", ExpectedResult = "Foo")]
		[TestCase("Foo", null, ExpectedResult = "Foo")]
		[TestCase("Foo", "Bar", ExpectedResult = "Foo")]
		public string CustomHostEnvironmentOverridesLocal(string environment, string local)
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
		public string LicenseKeyEnvironmentOverridesLocal(string appSettingEnvironmentName, string newEnvironmentName, string legacyEnvironmentName, string local)
		{
			_localConfig.service.licenseKey = local;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_LICENSE_KEY")).Returns(newEnvironmentName);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEWRELIC_LICENSEKEY")).Returns(legacyEnvironmentName);
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.LicenseKey")).Returns(appSettingEnvironmentName);

			return _defaultConfig.AgentLicenseKey;
		}

		[Test]
		public void UrlRegexRulesPullsValueFromServerConfiguration()
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
		public void UrlRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] string input, [NotNull] string expectedOutput)
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
		public void MetricNameRegexRulesPullsValueFromServerConfiguration()
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
		public void MetricNameRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] string input, [NotNull] string expectedOutput)
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
		public void TransactionNameRegexRulesPullsValueFromServerConfiguration()
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
		public void TransactionNameRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle([NotNull] string input, [NotNull] string expectedOutput)
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
		public void WebTransactionsApdexPullsValueFromServerConfiguration()
		{
			_serverConfig.WebTransactionsApdex = new Dictionary<string, double>
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
		public void TransactionNameWhitelistRulesPullsValueFromServerConfiguration()
		{
			_serverConfig.TransactionNameWhitelistRules = new List<ServerConfiguration.WhitelistRule>
			{
				new ServerConfiguration.WhitelistRule
				{
					Prefix = "apple/banana",
					Terms = new List<string> {"pie", "cake"}
				},
				new ServerConfiguration.WhitelistRule
				{
					Prefix = "mango/peach/",
					Terms = new List<string>()
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
		public void ApplicationNamesThrowsIfNoAppNameFound()
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
		public void ApplicationNamesPullsNamesFromRuntimeConfig()
		{
			_runTimeConfig.ApplicationNames = new List<string> { "MyAppName1", "MyAppName2" };
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((string)null);
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((string)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns((string)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns((string)null);
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
		public void ApplicationNamesPullsSingleNameFromAppSettings()
		{
			_runTimeConfig.ApplicationNames = new List<string>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns("MyAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
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
		public void ApplicationNamesPullsMultipleNamesFromAppSettings()
		{
			_runTimeConfig.ApplicationNames = new List<string>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns("MyAppName1,MyAppName2");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
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
		public void ApplicationNamesPullsSingleNameFromIisExpressSitenameEnvironmentVariable()
		{
			_runTimeConfig.ApplicationNames = new List<string>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((string)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
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
		public void ApplicationNamesPullsMultipleNamesFromIisExpressSitenameEnvironmentVariaible()
		{
			_runTimeConfig.ApplicationNames = new List<string>();
			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns((string)null);
			Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName1,MyAppName2");
			Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
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
		public void ApplicationNamesPullsSingleNameFromRoleNameEnvironmentVariaible()
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
		public void ApplicationNamesPullsMultipleNamesFromRoleNameEnvironmentVariaible()
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
		public void ApplicationNamesPullsNamesFromNewRelicConfig()
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
		public void ApplicationNamesPullsSingleNameFromAppPoolIdEnvironmentVariaible()
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
		public void ApplicationNamesPullsMultipleNamesFromAppPoolIdEnvironmentVariaible()
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
		public void ApplicationNamesPullsNameFromProcessIdIfAppDomainAppVirtualPathIsNull()
		{
			_runTimeConfig.ApplicationNames = new List<string>();

			//Sets to default return null for all calls unless overriden by later arrange.
			Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

			Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);
			
			_localConfig.application.name = new List<string>();

			Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns<string>(null);
			Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("MyAppName");

			NrAssert.Multiple(
				() => Assert.AreEqual(1, _defaultConfig.ApplicationNames.Count()),
				() => Assert.AreEqual("MyAppName", _defaultConfig.ApplicationNames.FirstOrDefault())
				);
		}


		#endregion ApplicationNames


		[Test]
		public void AutostartAgentPullsFromLocalConfig()
		{
			_localConfig.service.autoStart = false;
			Assert.IsFalse(_defaultConfig.AutoStartAgent);

			_localConfig.service.autoStart = true;
			Assert.IsTrue(_defaultConfig.AutoStartAgent);
		}

		#region CrossApplicationTracingEnabled

		[Test]
		public void CrossApplicationTracingEnabledIsTrueIfAllCatFlagsEnabledAndCrossProcessIdIsNotNull()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsTrueIfCrossApplicationTracerIsMissingButAllOtherFlagsEnabled()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer = null;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsFalseIfCrossApplicationTracingEnabledIsFalse()
		{
			_localConfig.crossApplicationTracingEnabled = false;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = "123#456";

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsFalseIfRpmConfigCrossApplicationTracerEnabledIsFalse()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = false;
			_serverConfig.CatId = "123#456";

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsFalseIfCatIdIsNull()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
			_serverConfig.CatId = null;

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsTrueWithNewServerConfig()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig = new ServerConfiguration();
			_serverConfig.CatId = "123#456";
			_defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
		}

		[Test]
		public void CrossApplicationTracingEnabledIsFalseWithGetDefaultServerConfig()
		{
			_localConfig.crossApplicationTracingEnabled = true;
			_localConfig.crossApplicationTracer.enabled = true;
			_serverConfig = ServerConfiguration.GetDefault();
			_serverConfig.CatId = "123#456";
			_defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
		}

		#endregion CrossApplicationTracingEnabled

		#region Utilization

		[Test]
		public void UtilizationDetectAwsIsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectAws);
		}

		[Test]
		public void UtilizationDetectAzureIsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectAzure);
		}

		[Test]
		public void UtilizationDetectPcfIsTrueByDefualt()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectPcf);
		}

		[Test]
		public void UtilizationDetectGcpIsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectGcp);
		}

		[Test]
		public void UtilizationDetectDockerIsTrueByDefault()
		{
			Assert.IsTrue(_defaultConfig.UtilizationDetectDocker);
		}

		[Test]
		public void UtilizationDetectAwsIsSetToFalse()
		{
			_localConfig.utilization.detectAws = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectAws);
		}

		[Test]
		public void UtilizationDetectAzureIsSetToFalse()
		{
			_localConfig.utilization.detectAzure = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectAzure);
		}

		[Test]
		public void UtilizationDetectPcfIsSetToFalse()
		{
			_localConfig.utilization.detectPcf = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectPcf);
		}

		[Test]
		public void UtilizationDetectGcpIsSetToFalse()
		{
			_localConfig.utilization.detectGcp = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectGcp);
		}

		[Test]
		public void UtilizationDetectDockerIsSetToFalse()
		{
			_localConfig.utilization.detectDocker = false;
			Assert.IsFalse(_defaultConfig.UtilizationDetectDocker);
		}

		#endregion

		#region Capture Attributes

		[TestCase(true, true)]
		[TestCase(false, false)]
		public void CaptureAttributes(bool captureAttributes, bool expectedResult)
		{
			_localConfig.attributes.enabled = captureAttributes;
			Assert.AreEqual(expectedResult, _defaultConfig.CaptureAttributes);
		}

		[TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureAttributuesIncludes(bool highSecurity, bool attributesEnabled, string[] attributes, string[] expectedResult)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.attributes.enabled = attributesEnabled;
			_localConfig.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureAttributesIncludes.Count());
		}

		[TestCase(false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureAttributuesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
		{
			SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
			_localConfig.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureAttributesIncludes.Count());
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void CaptureTransactionEventsAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
		{
			_localConfig.attributes.enabled = globalAttributes;
			_localConfig.transactionEvents.attributes.enabled = localAttributes;
			Assert.AreEqual(expectedResult, _defaultConfig.CaptureTransactionEventsAttributes);
		}


		[TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureTransactionEventAttributesIncludes(bool highSecurity, bool localAttributesEnabled, string[] attributes, string[] expectedResult)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.transactionEvents.attributes.enabled = localAttributesEnabled;
			_localConfig.transactionEvents.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureTransactionEventAttributesIncludes.Count());
		}

		[TestCase(false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureTransactionEventAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
		{
			SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
			_localConfig.transactionEvents.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureTransactionEventAttributesIncludes.Count());
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void CaptureTransactionTraceAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
		{
			_localConfig.attributes.enabled = globalAttributes;
			_localConfig.transactionTracer.attributes.enabled = localAttributes;
			Assert.AreEqual(expectedResult, _defaultConfig.CaptureTransactionTraceAttributes);
		}


		[TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureTransactionTraceAttributesIncludes(bool highSecurity, bool localAttributesEnabled, string[] attributes, string[] expectedResult)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.transactionTracer.attributes.enabled = localAttributesEnabled;
			_localConfig.transactionTracer.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureTransactionTraceAttributesIncludes.Count());
		}

		[TestCase(false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureTransactionTraceAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
		{
			SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
			_localConfig.transactionTracer.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureTransactionTraceAttributesIncludes.Count());
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void CaptureErrorCollectorAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
		{
			_localConfig.attributes.enabled = globalAttributes;
			_localConfig.errorCollector.attributes.enabled = localAttributes;
			Assert.AreEqual(expectedResult, _defaultConfig.CaptureErrorCollectorAttributes);
		}


		[TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureErrorCollectorAttributesIncludes(bool highSecurity, bool localAttributesEnabled, string[] attributes, string[] expectedResult)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.errorCollector.attributes.enabled = localAttributesEnabled;
			_localConfig.errorCollector.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureErrorCollectorAttributesIncludes.Count());
		}

		[TestCase(false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureErrorCollectorAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
		{
			SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
			_localConfig.errorCollector.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureErrorCollectorAttributesIncludes.Count());
		}

		[TestCase(false, false, false)]
		[TestCase(false, true, false)]
		[TestCase(true, true, true)]
		[TestCase(true, false, false)]
		public void CaptureBrowserMonitoringAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
		{
			_localConfig.attributes.enabled = globalAttributes;
			_localConfig.browserMonitoring.attributes.enabled = localAttributes;
			Assert.AreEqual(expectedResult, _defaultConfig.CaptureBrowserMonitoringAttributes);
		}

		[TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureBrowserMonitoringAttributesIncludes(bool highSecurity,bool localAttributesEnabled, string[] attributes, string[] expectedResult)
		{
			_localConfig.highSecurity.enabled = highSecurity;
			_localConfig.browserMonitoring.attributes.enabled = localAttributesEnabled;
			_localConfig.browserMonitoring.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureBrowserMonitoringAttributesIncludes.Count());
		}

		[TestCase(false, new[] { "att1", "att2" }, new string[] { })]
		[TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
		public void CaptureBrowserMonitoringAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
		{
			SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
			_localConfig.browserMonitoring.attributes.enabled = true;
			_localConfig.browserMonitoring.attributes.include = new List<string>(attributes);

			Assert.AreEqual(expectedResult.Length, _defaultConfig.CaptureBrowserMonitoringAttributesIncludes.Count());
		}

		[TestCase(true, true, true, ExpectedResult = false)]
		[TestCase(true, true, false, ExpectedResult = false)]
		[TestCase(true, true, null, ExpectedResult = false)]
		[TestCase(true, false, true, ExpectedResult = false)]
		[TestCase(true, false, false, ExpectedResult = false)]
		[TestCase(true, false, null, ExpectedResult = false)]
		[TestCase(false, true, true, ExpectedResult = true)]
		[TestCase(false, true, false, ExpectedResult = false)]
		[TestCase(false, true, null, ExpectedResult = true)]
		[TestCase(false, false, true, ExpectedResult = false)]
		[TestCase(false, false, false, ExpectedResult = false)]
		[TestCase(false, false, null, ExpectedResult = false)]
		public bool CustomEventsEnabledShouldHonorConfiguration(bool isHighSecurity, bool localConfigValue, bool? serverConfigValue)
		{
			_localConfig.customEvents.enabled = localConfigValue;
			_localConfig.highSecurity.enabled = isHighSecurity;
			_serverConfig.CustomEventCollectionEnabled = serverConfigValue;

			return _defaultConfig.CustomEventsEnabled;
		}

		[TestCase(true, true, true, ExpectedResult = true)]
		[TestCase(true, true, false, ExpectedResult = false)]
		[TestCase(true, false, true, ExpectedResult = false)]
		[TestCase(true, false, false, ExpectedResult = false)]
		[TestCase(false, true, true, ExpectedResult = false)]
		[TestCase(false, true, false, ExpectedResult = false)]
		[TestCase(false, false, true, ExpectedResult = false)]
		[TestCase(false, false, false, ExpectedResult = false)]
		[TestCase(true, null, true, ExpectedResult = true)]
		[TestCase(true, null, false, ExpectedResult = false)]
		[TestCase(false, null, true, ExpectedResult = false)]
		[TestCase(false, null, false, ExpectedResult = false)]
		public bool CustomEventsEnabledMostSecureWinsWithSecurityPolicies(bool localEnabled, bool? serverEnabled, bool securityPoliciesEnabled)
		{
			SetupNewConfigsWithSecurityPolicy("custom_events", securityPoliciesEnabled);
			_localConfig.customEvents.enabled = localEnabled;
			_serverConfig.CustomEventCollectionEnabled = serverEnabled;

			return _defaultConfig.CustomEventsEnabled;
		}

		#endregion

		#region SecurityPolicies

		[TestCase(null, null, ExpectedResult = "")]
		[TestCase(null, "localValue", ExpectedResult = "localValue")]
		[TestCase("envValue", null, ExpectedResult = "envValue")]
		[TestCase("envValue", "localValue", ExpectedResult = "envValue")]
		[TestCase("", "localValue", ExpectedResult = "")]
		[TestCase("  ", "localValue", ExpectedResult = "")]
		[TestCase("  envValue  ", null, ExpectedResult = "envValue")]
		[TestCase(null, "  localValue  ", ExpectedResult = "localValue")]
		public string SecurityPoliciesTokenReturned(string environmentValue, string localConfigValue)
		{
			_localConfig.securityPoliciesToken = localConfigValue;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_SECURITY_POLICIES_TOKEN"))
				.Returns(environmentValue);
			return _defaultConfig.SecurityPoliciesToken;
		}

		[TestCase(null, null, ExpectedResult = false)]
		[TestCase(null, "localValue", ExpectedResult = true)]
		[TestCase("envValue", null, ExpectedResult = true)]
		[TestCase("envValue", "localValue", ExpectedResult = true)]
		[TestCase("", "localValue", ExpectedResult = false)]    // non-intuitive result, but this behavior is
		// consistent across all Env Var configs
		[TestCase("envValue", "", ExpectedResult = true)]
		[TestCase("", "", ExpectedResult = false)]
		public bool SecurityPoliciesTokenExists(string environmentValue, string localConfigValue)
		{
			_localConfig.securityPoliciesToken = localConfigValue;
			Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_SECURITY_POLICIES_TOKEN"))
				.Returns(environmentValue);
			_defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic);

			return _defaultConfig.SecurityPoliciesTokenExists;
		}

		#endregion SecurityPolicies
	}
}
