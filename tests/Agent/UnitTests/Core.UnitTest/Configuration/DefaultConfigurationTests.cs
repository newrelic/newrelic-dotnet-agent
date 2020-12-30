// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.SystemInterfaces;
using NewRelic.SystemInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration.UnitTest
{
    internal class TestableDefaultConfiguration : DefaultConfiguration
    {
        public TestableDefaultConfiguration(IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic)
            : base(environment, localConfig, serverConfig, runTimeConfiguration, securityPoliciesConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic) { }
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
        private IDnsStatic _dnsStatic;

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
            _dnsStatic = Mock.Create<IDnsStatic>();

            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
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

        [TestCase(null, null, true, ExpectedResult = true)]
        [TestCase(null, null, false, ExpectedResult = false)]
        [TestCase(null, true, true, ExpectedResult = true)]
        [TestCase(null, true, false, ExpectedResult = false)]
        [TestCase(null, false, true, ExpectedResult = false)]
        [TestCase(null, false, false, ExpectedResult = false)]
        [TestCase(true, null, true, ExpectedResult = true)]
        [TestCase(true, null, false, ExpectedResult = false)]
        [TestCase(true, true, true, ExpectedResult = true)]
        [TestCase(true, true, false, ExpectedResult = false)]
        [TestCase(true, false, true, ExpectedResult = false)]
        [TestCase(true, false, false, ExpectedResult = false)]
        [TestCase(false, null, true, ExpectedResult = false)]
        [TestCase(false, null, false, ExpectedResult = false)]
        [TestCase(false, true, true, ExpectedResult = false)]
        [TestCase(false, true, false, ExpectedResult = false)]
        [TestCase(false, false, true, ExpectedResult = false)]
        [TestCase(false, false, false, ExpectedResult = false)]
        public bool TransactionEventsCanBeDisbledByServer(bool? server, bool? legacyLocal, bool local)
        {
            _localConfig.transactionEvents.enabled = local;

            _localConfig.analyticsEvents.enabled = legacyLocal ?? default(bool);
            _localConfig.analyticsEvents.enabledSpecified = legacyLocal.HasValue;

            _serverConfig.AnalyticsEventCollectionEnabled = server;

            return _defaultConfig.TransactionEventsEnabled;
        }

        [Test]
        public void EveryConfigShouldGetNewVersionNumber()
        {
            var newConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

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
        public void WhenConfigsAreDefaultThenInstanceReportingEnabledIsEnabled()
        {
            Assert.IsTrue(_defaultConfig.InstanceReportingEnabled);
        }

        [Test]
        public void WhenConfigsAreDefaultThenDatabaseNameReportingEnabledIsEnabled()
        {
            Assert.IsTrue(_defaultConfig.DatabaseNameReportingEnabled);
        }

        [Test]
        public void WhenConfigsAreDefaultThenDatastoreTracerQueryParametersEnabledIsDisabled()
        {
            Assert.IsFalse(_defaultConfig.DatastoreTracerQueryParametersEnabled);
        }

        [TestCase(true, false, false, false, configurationTransactionTracerRecordSql.raw, ExpectedResult = true)]
        [TestCase(true, false, false, false, configurationTransactionTracerRecordSql.obfuscated, ExpectedResult = false)]
        [TestCase(true, false, false, false, configurationTransactionTracerRecordSql.off, ExpectedResult = false)]
        [TestCase(true, true, false, false, configurationTransactionTracerRecordSql.raw, ExpectedResult = false)]
        [TestCase(true, false, true, true, configurationTransactionTracerRecordSql.raw, ExpectedResult = false)]
        [TestCase(true, false, true, false, configurationTransactionTracerRecordSql.raw, ExpectedResult = false)]
        [TestCase(false, false, false, false, configurationTransactionTracerRecordSql.raw, ExpectedResult = false)]
        public bool DatastoreTracerQueryParametersEnabledRespectsHighSecurityModeAndSecurityPolicy(
            bool queryParametersEnabled,
            bool highSecurityModeEnabled,
            bool securityPolicyEnabled,
            bool recordSqlSecurityPolicyEnabled,
            configurationTransactionTracerRecordSql recordSqlSetting)
        {
            _localConfig.datastoreTracer.queryParameters.enabled = queryParametersEnabled;
            _localConfig.highSecurity.enabled = highSecurityModeEnabled;
            _localConfig.transactionTracer.recordSql = recordSqlSetting;
            if (securityPolicyEnabled)
            {
                SetupNewConfigsWithSecurityPolicy("record_sql", recordSqlSecurityPolicyEnabled);
            }
            return _defaultConfig.DatastoreTracerQueryParametersEnabled;
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
        public void WhenConfigsAreDefaultThenCaptureAgentTimingIsDisabled()
        {
            Assert.AreEqual(false, _defaultConfig.DiagnosticsCaptureAgentTiming);
        }

        [Test]
        public void WhenTransactionEventsAreDisabledInLocalConfigAndDoNotExistInServerConfigThenTransactionEventsAreDisabled()
        {
            _localConfig.transactionEvents.enabled = false;
            Assert.IsFalse(_defaultConfig.TransactionEventsEnabled);
        }

        [Test]
        public void TransactionEventsMaxSamplesStoredPassesThroughToLocalConfig()
        {
            Assert.AreEqual(10000, _defaultConfig.TransactionEventsMaximumSamplesStored);

            _localConfig.transactionEvents.maximumSamplesStored = 10001;
            Assert.AreEqual(10001, _defaultConfig.TransactionEventsMaximumSamplesStored);

            _localConfig.transactionEvents.maximumSamplesStored = 9999;
            Assert.AreEqual(9999, _defaultConfig.TransactionEventsMaximumSamplesStored);
        }

        [Test]
        public void TransactionEventsMaxSamplesStoredOverriddenByEventHarvestConfig()
        {
            _localConfig.transactionEvents.maximumSamplesStored = 10001;
            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.TransactionEventHarvestLimitKey, 10 } }
            };

            Assert.AreEqual(10, _defaultConfig.TransactionEventsMaximumSamplesStored);
        }

        [TestCase("10", 20, 30, ExpectedResult = 10)]
        [TestCase("10", null, 30, ExpectedResult = 10)]
        [TestCase("10", 20, null, ExpectedResult = 10)]
        [TestCase("10", null, null, ExpectedResult = 10)]
        [TestCase(null, 20, 30, ExpectedResult = 30)]
        [TestCase(null, null, 30, ExpectedResult = 30)]
        [TestCase(null, 20, null, ExpectedResult = 20)]
        [TestCase(null, null, null, ExpectedResult = 10000)]
        public int TransactionEventsMaxSamplesStoredOverriddenByEnvironment(string environmentSetting, int? localSetting, int? serverSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("MAX_TRANSACTION_SAMPLES_STORED")).Returns(environmentSetting);

            if (localSetting != null)
            {
                _localConfig.transactionEvents.maximumSamplesStored = (int)localSetting;
            }

            if (serverSetting != null)
            {
                _serverConfig.EventHarvestConfig = new EventHarvestConfig
                {
                    ReportPeriodMs = 5000,
                    HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.TransactionEventHarvestLimitKey, (int)serverSetting } }
                };
            }

            return _defaultConfig.TransactionEventsMaximumSamplesStored;
        }

        [Test]
        public void TransactionEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), _defaultConfig.TransactionEventsHarvestCycle);

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.TransactionEventHarvestLimitKey, 10 } }
            };
            Assert.AreEqual(TimeSpan.FromSeconds(5), _defaultConfig.TransactionEventsHarvestCycle);
        }

        [Test]
        public void TransactionEventsMaxSamplesOf0ShouldDisableTransactionEvents()
        {
            _localConfig.transactionEvents.maximumSamplesStored = 0;
            Assert.IsFalse(_defaultConfig.TransactionEventsEnabled);
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

        [Test]
        public void DiagnosticsCaptureAgentTimingSetFromLocal
        ([Values(true, false, null)] bool? localIsEnabled,
            [Values(10, 100, 0, -1, null)] int? localFrequency
        )
        {
            var expectedIsEnabled = localIsEnabled.GetValueOrDefault() && localFrequency.GetValueOrDefault(1) > 0;
            var expectedFrequency = localFrequency.GetValueOrDefault(1);

            if (localIsEnabled.HasValue)
            {
                _localConfig.diagnostics.captureAgentTiming = localIsEnabled.Value;
            }

            if (localFrequency.HasValue)
            {
                _localConfig.diagnostics.captureAgentTimingFrequency = localFrequency.Value;
            }

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedIsEnabled, _defaultConfig.DiagnosticsCaptureAgentTiming, "Performance Timing Enabled"),
                () => Assert.AreEqual(expectedFrequency, _defaultConfig.DiagnosticsCaptureAgentTimingFrequency, "Perforamcne Timing Frequency")
            );
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
        [TestCase(false, true, ExpectedResult = false)]
        [TestCase(true, true, ExpectedResult = true)]
        [TestCase(false, false, ExpectedResult = false)]
        public bool ErrorCollectorCaptureEventsConditionalOverrideFromServer(bool local, bool server)
        {
            _localConfig.errorCollector.captureEvents = local;
            _serverConfig.ErrorEventCollectionEnabled = server;

            return _defaultConfig.ErrorCollectorCaptureEvents;
        }

        [TestCase(50, ExpectedResult = 50)]
        public int ErrorCollectorMaxNumberEventSamplesSetFromLocal(int local)
        {
            _localConfig.errorCollector.maxEventSamplesStored = local;
            return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
        }

        [TestCase(ExpectedResult = 100)]
        public int ErrorCollectorMaxNumberEventSamplesDefaultFromLocal()
        {
            return _defaultConfig.ErrorCollectorMaxEventSamplesStored;
        }

        [Test]
        public void ErrorEventsMaxSamplesStoredOverriddenByEventHarvestConfig()
        {
            _localConfig.errorCollector.maxEventSamplesStored = 10001;
            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.ErrorEventHarvestLimitKey, 10 } }
            };

            Assert.AreEqual(10, _defaultConfig.ErrorCollectorMaxEventSamplesStored);
        }

        [Test]
        public void ErrorEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), _defaultConfig.ErrorEventsHarvestCycle);

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.ErrorEventHarvestLimitKey, 10 } }
            };
            Assert.AreEqual(TimeSpan.FromSeconds(5), _defaultConfig.ErrorEventsHarvestCycle);
        }

        [Test]
        public void ErrorEventsMaxSamplesOf0ShouldDisableErrorEvents()
        {
            _localConfig.errorCollector.maxEventSamplesStored = 0;
            Assert.IsFalse(_defaultConfig.ErrorCollectorCaptureEvents);
        }

        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool BrowserMonitoringCaptureAttributesSetFromLocal(bool local)
        {
            _localConfig.browserMonitoring.captureAttributes = local;

            return _defaultConfig.CaptureBrowserMonitoringAttributes;
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

        [TestCase(new[] { "local" }, new[] { "server" }, "request.parameters.server")]
        [TestCase(new[] { "local" }, null, "request.parameters.local")]
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
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
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

            Assert.AreEqual(42 * 4, _defaultConfig.TransactionTraceThreshold.TotalSeconds);
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

            if (customParametersSpecified)
            {
                _localConfig.customParameters.enabledSpecified = customParametersSpecified;
                _localConfig.customParameters.enabled = customParametersEnabled;
            }

            return _defaultConfig.CaptureCustomParameters;
        }

        [Test]
        public void CustomParametersToIgnoreSetFromLocal()
        {
            _localConfig.parameterGroups.customParameters.ignore = new List<string>() { "local" };

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
            _localConfig.parameterGroups.identityParameters.ignore = new List<string>() { "local" };

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
            _localConfig.parameterGroups.responseHeaderParameters.ignore = new List<string>() { "local" };

            Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.local"));
        }

        [TestCase(new[] { "local" }, new[] { "server" }, ExpectedResult = "server")]
        [TestCase(new[] { "local" }, null, ExpectedResult = "local")]
        public string ExceptionsToIgnoreSetFromLocalAndServerOverrides(string[] local, string[] server)
        {
            _serverConfig.RpmConfig.ErrorCollectorErrorsToIgnore = server;
            _localConfig.errorCollector.ignoreErrors.exception = new List<string>(local);

            CreateDefaultConfiguration();

            return _defaultConfig.IgnoreErrorsConfiguration.Keys.FirstOrDefault();
        }

        [Test]
        public void Decodes_IngoreAndExpectedClasses_IgnoreAndExpectedMessages_ExpectedStatusCodes_Configurations_Successfully()
        {
            const string xmlString = @"<?xml version=""1.0""?>
<configuration xmlns=""urn:newrelic-config"" agentEnabled=""true"">
  <service licenseKey=""REPLACE_WITH_LICENSE_KEY"" ssl=""true"" />
  <errorCollector enabled=""true"" captureEvents=""true"" maxEventSamplesStored=""100"">
    <ignoreClasses>
        <errorClass>ErrorClass1</errorClass>
        <errorClass>ErrorClass2</errorClass>
    </ignoreClasses>
    <ignoreMessages>
        <errorClass name=""ErrorClass2"">
            <message>error message 1 in ErrorClass2</message>
        </errorClass>
        <errorClass name=""ErrorClass3"">
            <message>error message 1 in ErrorClass3</message>
            <message>error message 2 in ErrorClass3</message>
        </errorClass>
    </ignoreMessages>
    <ignoreStatusCodes>
        <code>404</code>
        <code>500</code>
    </ignoreStatusCodes>
    <expectedClasses>
        <errorClass>ErrorClass1</errorClass>
        <errorClass>ErrorClass2</errorClass>
    </expectedClasses>
    <expectedStatusCodes>404,500</expectedStatusCodes>
    <expectedMessages>
        <errorClass name=""ErrorClass2"">
            <message>error message 1 in ErrorClass2</message>
        </errorClass>
        <errorClass name=""ErrorClass3"">
            <message>error message 1 in ErrorClass3</message>
            <message>error message 2 in ErrorClass3</message>
        </errorClass>
    </expectedMessages>
  </errorCollector >
 </configuration>";
            var root = new XmlRootAttribute { ElementName = "configuration", Namespace = "urn:newrelic-config" };
            var serializer = new XmlSerializer(typeof(configuration), root);

            configuration localConfiguration;
            using (var reader = new StringReader(xmlString))
            {
                localConfiguration = serializer.Deserialize(reader) as configuration;
            }

            _defaultConfig = new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            CollectionAssert.AreEquivalent(_defaultConfig.ExpectedErrorStatusCodesForAgentSettings, new[] { "404", "500" });

            var expectedMessages = _defaultConfig.ExpectedErrorsConfiguration;

            Assert.That(expectedMessages.ContainsKey("ErrorClass1"));

            var expectedErrorClass2 = expectedMessages.Where(x => x.Key == "ErrorClass2").FirstOrDefault();
            Assert.NotNull(expectedErrorClass2);
            Assert.IsFalse(expectedErrorClass2.Value.Any());

            var expectedErrorClass3 = expectedMessages.Where(x => x.Key == "ErrorClass3").FirstOrDefault();
            Assert.NotNull(expectedErrorClass3);
            Assert.That(expectedErrorClass3.Value.Contains("error message 1 in ErrorClass3"));
            Assert.That(expectedErrorClass3.Value.Contains("error message 2 in ErrorClass3"));

            var ignoreMessages = _defaultConfig.IgnoreErrorsConfiguration;

            Assert.That(ignoreMessages.ContainsKey("ErrorClass1"));

            var ignoreErrorClass2 = ignoreMessages.Where(x => x.Key == "ErrorClass2").FirstOrDefault();
            Assert.NotNull(ignoreErrorClass2);
            Assert.IsFalse(ignoreErrorClass2.Value.Any());

            var ignoreErrorClass3 = ignoreMessages.Where(x => x.Key == "ErrorClass3").FirstOrDefault();
            Assert.NotNull(ignoreErrorClass3);
            Assert.That(ignoreErrorClass3.Value.Contains("error message 1 in ErrorClass3"));
            Assert.That(ignoreErrorClass3.Value.Contains("error message 2 in ErrorClass3"));

            Assert.That(_defaultConfig.IgnoreErrorsConfiguration.ContainsKey("404"));
            Assert.That(_defaultConfig.IgnoreErrorsConfiguration.ContainsKey("500"));
        }

        [TestCase(new[] { "local" }, new[] { "server" }, ExpectedResult = "server,server")]
        [TestCase(new[] { "local" }, null, ExpectedResult = "local,local")]
        public string IgnoreAndExpectedClassesSetFromLocalAndServerOverrides(string[] local, string[] server)
        {
            _serverConfig.RpmConfig.ErrorCollectorExpectedClasses = server;
            _localConfig.errorCollector.expectedClasses.errorClass = new List<string>(local);

            _serverConfig.RpmConfig.ErrorCollectorIgnoreClasses = server;
            _localConfig.errorCollector.ignoreClasses.errorClass = new List<string>(local);

            CreateDefaultConfiguration();

            return _defaultConfig.ExpectedErrorsConfiguration.FirstOrDefault().Key + "," + _defaultConfig.IgnoreErrorsConfiguration.FirstOrDefault().Key;
        }

        [TestCase(new[] { "Class1", "Class2" }, new[] { "Class1" }, ExpectedResult = "Class1,Class2")]
        [TestCase(new[] { "Class1" }, new[] { "Class2" }, ExpectedResult = "Class1,Class2")]
        public string IgnoreErrorsAndIgnoreClassesCombineTests(string[] ignoreClasses, string[] ignoreErrors)
        {
            _localConfig.errorCollector.ignoreClasses.errorClass = new List<string>(ignoreClasses);
            _localConfig.errorCollector.ignoreErrors.exception = new List<string>(ignoreErrors);

            CreateDefaultConfiguration();
            return string.Join(",", _defaultConfig.IgnoreErrorsConfiguration.Keys);
        }



        [TestCase("401", new[] { "405" }, ExpectedResult = new[] { "405" })]
        [TestCase("401", new string[0], ExpectedResult = new string[0])]
        [TestCase("401", null, ExpectedResult = new[] { "401" })]
        public IEnumerable<object> ExpectedStatusCodesSetFromLocalAndServerOverrides(string local, string[] server)
        {
            _serverConfig.RpmConfig.ErrorCollectorExpectedStatusCodes = server;
            _localConfig.errorCollector.expectedStatusCodes = (local);

            CreateDefaultConfiguration();

            return _defaultConfig.ExpectedErrorStatusCodesForAgentSettings;
        }

        [TestCase("401-404", new string[] { "401.5", "402.3"}, new bool[] { false, false})] //does not support full status codes
        [TestCase("400,401,404", new string[]{"400", "401", "402", "403", "404"},  new bool[] { true, true, false, false, true })]
        [TestCase("400, 401 ,404", new string[] { "400", "401", "402", "403", "404" }, new bool[] { true, true, false, false, true })]
        [TestCase("400, 401,404, ", new string[] { "400", "401", "402", "403", "404" }, new bool[] { true, true, false, false, true })]
        [TestCase("400,401-404", new string[] { "400", "401", "402", "403", "404" }, new bool[] { true, true, true, true, true })]
        [TestCase("402,401-404", new string[] { "400", "401", "402", "403", "404" }, new bool[] { false, true, true, true, true })]
        [TestCase("401.4,401", new string[] { "400", "401", "402", "403", "404" }, new bool[] { false, true, false, false, false })] //does not support full status codes
        [TestCase("404,401.4", new string[] { "400", "401", "402", "403", "404" }, new bool[] { false, false, false, false, true })]
        [TestCase("401.4-404.5", new string[] { "400", "401", "402", "403", "404" }, new bool[] { false, false, false, false, false })] //does not support full status codes
        [TestCase("Foo,Bar,X-Y", new string[] { "400", "401" }, new bool[] { false, false })] //does not work with non status code values
        public void ExpectedStatusCodesParserTests(string local, string[] inputs, bool[] expected)
        {
            _localConfig.errorCollector.expectedStatusCodes = local;

            CreateDefaultConfiguration();

            var actual = new bool[inputs.Length];
            for (var i = 0; i < inputs.Length; i++)
            {
                actual[i] = _defaultConfig.ExpectedStatusCodes.Any(u => u.IsMatch(inputs[i]));
            }

            CollectionAssert.AreEqual(expected, actual);
            
        }

        [TestCase(true, ExpectedResult = "server,server")]
        [TestCase(false, ExpectedResult = "local,local")]
        public string IgnoreAndExpectedMessagesSetFromLocalAndServerOverrides(bool server)
        {
            if (server)
            {
                _serverConfig.RpmConfig.ErrorCollectorExpectedMessages = new List<KeyValuePair<string, IEnumerable<string>>>
                {
                    new KeyValuePair<string, IEnumerable<string>> ("server", new List<string>())
                };

                _serverConfig.RpmConfig.ErrorCollectorIgnoreMessages = new List<KeyValuePair<string, IEnumerable<string>>>
                {
                    new KeyValuePair<string, IEnumerable<string>> ("server", new List<string>())
                };
            }

            _localConfig.errorCollector.expectedMessages = new List<ErrorMessagesCollectionErrorClass>()
            {
                new ErrorMessagesCollectionErrorClass() {name = "local"}
            };

            _localConfig.errorCollector.ignoreMessages = new List<ErrorMessagesCollectionErrorClass>()
            {
                new ErrorMessagesCollectionErrorClass() {name = "local"}
            };

            CreateDefaultConfiguration();

            return _defaultConfig.ExpectedErrorsConfiguration.FirstOrDefault().Key + "," + _defaultConfig.IgnoreErrorsConfiguration.FirstOrDefault().Key;
        }

        //password = "XYZ" obscuring-key = "123"  encrypted-password = "aWtp"
        //password = "XYZ" obscuring-key = "456"  encrypted-password = "bGxs"

        [TestCase("ABCD", "aWtp", "123", null, ExpectedResult = "XYZ")]
        [TestCase("ABCD", "bGxs", null, "456", ExpectedResult = "XYZ")]
        [TestCase("ABCD", "bGxs", "123", "456", ExpectedResult = "XYZ")]
        [TestCase("ABCD", "bGxs", null, null, ExpectedResult = "ABCD")]
        [TestCase("ABCD", null, "123", null, ExpectedResult = "ABCD")]
        [TestCase("ABCD", "   ", "123", null, ExpectedResult = "ABCD")]
        [TestCase("ABCD", "bGxs", "   ", null, ExpectedResult = "ABCD")]
        public string Encrypting_Decrypting_ProxyPassword_Tests(string password, string passwordObfuscated, string localConfigObscuringKey, string envObscuringKey)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_CONFIG_OBSCURING_KEY")).Returns(envObscuringKey);

            _localConfig.service.proxy.password = password;
            _localConfig.service.obscuringKey = localConfigObscuringKey;
            _localConfig.service.proxy.passwordObfuscated = passwordObfuscated;
            
            CreateDefaultConfiguration();

            return _defaultConfig.ProxyPassword;
        }

        [Test]
        public void ExpectedErrorSettingsForAgentSettingsReportedCorrectly()
        {
            _localConfig.errorCollector.expectedStatusCodes = "404,500";
            _localConfig.errorCollector.expectedClasses.errorClass = new List<string> { "ErrorClass1", "ErrorClass2" };
            _localConfig.errorCollector.expectedMessages = new List<ErrorMessagesCollectionErrorClass>
            {
                new ErrorMessagesCollectionErrorClass
                {
                    name = "ErrorClass2",
                    message = new List<string> { "error message 1 in ErrorClass2" }
                },
                new ErrorMessagesCollectionErrorClass
                {
                    name = "ErrorClass3",
                    message = new List<string> { "error message 1 in ErrorClass3", "error message 2 in ErrorClass3" }
                },
            };

            CreateDefaultConfiguration();

            var expectedStatusCodes = new string[] { "404", "500" };
            var expectedErrorClasses = new[] { "ErrorClass1", "ErrorClass2" };
            var expectedErrorMessages = new Dictionary<string, IEnumerable<string>>
            {
                { "ErrorClass2", new[] { "error message 1 in ErrorClass2" }  },
                { "ErrorClass3", new[] { "error message 1 in ErrorClass3", "error message 2 in ErrorClass3" } }
            };

            NrAssert.Multiple(
                () => CollectionAssert.AreEquivalent(expectedStatusCodes, _defaultConfig.ExpectedErrorStatusCodesForAgentSettings),
                () => CollectionAssert.AreEquivalent(expectedErrorClasses, _defaultConfig.ExpectedErrorClassesForAgentSettings),
                () => CollectionAssert.AreEquivalent(expectedErrorMessages, _defaultConfig.ExpectedErrorMessagesForAgentSettings)
            );
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

        [TestCase(new float[] { 400, 404 }, new[] { "500" }, ExpectedResult = "500")]
        [TestCase(new float[] { 400, 404 }, null, ExpectedResult = "400")]
        public string StatusCodesToIgnoreSetFromLocalAndServerOverrides(float[] local, string[] server)
        {
            _serverConfig.RpmConfig.ErrorCollectorStatusCodesToIgnore = server;
            _localConfig.errorCollector.ignoreStatusCodes.code = new List<float>(local);
            CreateDefaultConfiguration();
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
            var root = new XmlRootAttribute { ElementName = "configuration", Namespace = "urn:newrelic-config" };
            var serializer = new XmlSerializer(typeof(configuration), root);

            configuration localConfiguration;
            using (var reader = new StringReader(xmlString))
            {
                localConfiguration = serializer.Deserialize(reader) as configuration;
            }

            _defaultConfig = new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

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

        [TestCase(true, ExpectedResult = true)]
        [TestCase(false, ExpectedResult = false)]
        public bool AnalyticsEventDeprecatedValueOverridesDefault(bool deprecatedEnabled)
        {
            _localConfig.analyticsEvents.captureAttributesSpecified = false;
            _localConfig.transactionEvents.attributes.enabled = deprecatedEnabled;

            return _defaultConfig.TransactionEventsAttributesEnabled;
        }

        [Test]
        public void TransactionEventUsesDefaultWhenNoConfigValues()
        {
            _localConfig.analyticsEvents.captureAttributesSpecified = false;

            Assert.IsTrue(_defaultConfig.TransactionEventsAttributesEnabled);
        }

        [Test]
        public void DeprecatedIgnoreIdentityParametersValueBecomesExclude()
        {
            _localConfig.parameterGroups.identityParameters.ignore = new List<string>() { "foo" };

            Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("identity.foo"));
        }

        [Test]
        public void DeprecatedIgnoreCustomParametersValueBecomesExclude()
        {
            _localConfig.parameterGroups.customParameters.ignore = new List<string>() { "foo" };

            Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("foo"));
        }

        [Test]
        public void DeprecatedIgnoreResponseHeaderParametersValueBecomesExclude()
        {
            _localConfig.parameterGroups.responseHeaderParameters.ignore = new List<string>() { "foo" };

            Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("response.headers.foo"));
        }

        [Test]
        public void DeprecatedIgnoreRequestHeaderParametersValueBecomesExclude()
        {
            _localConfig.parameterGroups.requestHeaderParameters.ignore = new List<string>() { "foo" };

            Assert.IsTrue(_defaultConfig.CaptureAttributesExcludes.Contains("request.headers.foo"));
        }

        [Test]
        public void Property_deprecated_ignore_requestParameters_value_becomes_exclude()
        {
            _localConfig.requestParameters.ignore = new List<string>() { "foo" };

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

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        public bool SpanEventsEnabledEnvironmentOverridesLocal(bool localSpanEvents, string environmentSpanEvents)
        {
            _localConfig.spanEvents.enabled = localSpanEvents;
            _localConfig.distributedTracing.enabled = true;

            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_SPAN_EVENTS_ENABLED")).Returns(environmentSpanEvents);

            return _defaultConfig.SpanEventsEnabled;
        }

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        public bool DistributedTracingEnabledEnvironmentOverridesLocal(bool localDistributedTracing, string environmentDistributedTracing)
        {
            _localConfig.distributedTracing.enabled = localDistributedTracing;

            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_DISTRIBUTED_TRACING_ENABLED")).Returns(environmentDistributedTracing);

            return _defaultConfig.DistributedTracingEnabled;
        }

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        public bool DisableSamplersEnvironmentOverridesLocal(bool localDisableSamplers, string environmentDisableSamplers)
        {
            _localConfig.application.disableSamplers = localDisableSamplers;

            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_DISABLE_SAMPLERS")).Returns(environmentDisableSamplers);

            return _defaultConfig.DisableSamplers;
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
        public void UrlRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle(string input, string expectedOutput)
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
        public void MetricNameRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle(string input, string expectedOutput)
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
        public void TransactionNameRegexRulesUpdatesReplaceRegexBackreferencesToDotNetStyle(string input, string expectedOutput)
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
        }

        [TestCase(null, null, ExpectedResult = "coconut")]
        [TestCase("blandAmericanCoconut", null, ExpectedResult = "blandAmericanCoconut")]
        [TestCase("blandAmericanCoconut", "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        [TestCase(null, "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        public string ProcessHostDisplayNameIsSetFromLocalConfigurationAndEnvironmentVariable(string localConfigurationValue, string environmentVariableValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_PROCESS_HOST_DISPLAY_NAME")).Returns(environmentVariableValue);
            Mock.Arrange(() => _dnsStatic.GetHostName()).Returns("coconut");

            _localConfig.processHost.displayName = localConfigurationValue;
            return _defaultConfig.ProcessHostDisplayName;
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

            _localConfig.application.name = new List<string> { "MyAppName1", "MyAppName2" };
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

        [TestCase("AppPoolId", "w3wp.exe -ap AppPoolId")]
        [TestCase("AppPoolId", "w3wp.exe -ap \"AppPoolId\"")]
        [TestCase("W3WP", "w3wp.exe -app \"NotAnAppPool\"")]
        [TestCase("W3WP", "w3wp.exe -ap")]
        [TestCase("W3WP", "w3wp.exe -ap ")]
        [TestCase("AppPoolId", "w3wp.exe -firstArg -ap \"AppPoolId\" -thirdArg")]
        public void ApplicationNamesPullsSingleNameFromAppPoolIdFromCommandLine(string expected, string commandLine)
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting("NewRelic.AppName")).Returns<string>(null);

            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetCommandLineArgs()).Returns(commandLine.Split(new[] { ' ' }));
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns<string>(null);
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("W3WP");

            NrAssert.Multiple(
                () => Assert.AreEqual(expected, _defaultConfig.ApplicationNames.FirstOrDefault())
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

        [Test]
        public void UseResourceBasedNamingIsEnabled()
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "NewRelic.UseResourceBasedNamingForWCF",
                value = "true"
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.IsTrue(defaultConfig.UseResourceBasedNamingForWCFEnabled);
        }

        [Test]
        public void UseResourceBasedNamingIsDisabledByDefault()
        {
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            Assert.IsFalse(defaultConfig.UseResourceBasedNamingForWCFEnabled);
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
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.IsTrue(_defaultConfig.CrossApplicationTracingEnabled);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsFalseWithGetDefaultServerConfig()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig = ServerConfiguration.GetDefault();
            _serverConfig.CatId = "123#456";
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.IsFalse(_defaultConfig.CrossApplicationTracingEnabled);
        }

        #endregion CrossApplicationTracingEnabled

        #region Distributed Tracing
        [Test]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(null, false)]
        public void DistributedTracingEnabled(bool localConfig, bool expectedResult)
        {
            _localConfig.distributedTracing.enabled = localConfig;
            Assert.AreEqual(expectedResult, _defaultConfig.DistributedTracingEnabled);
        }

        [Test]
        public void DistributedTracingEnabledIsFalseByDefault()
        {
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.IsFalse(_defaultConfig.DistributedTracingEnabled);
        }

        [Test]
        [TestCase(null, null)]
        [TestCase("ApplicationIdValue", "ApplicationIdValue")]
        public void PrimaryApplicationIdValue(string server, string expectedResult)
        {
            _serverConfig.PrimaryApplicationId = server;

            Assert.AreEqual(_defaultConfig.PrimaryApplicationId, expectedResult);
        }

        [Test]
        [TestCase(null, null)]
        [TestCase("TrustedAccountKey", "TrustedAccountKey")]
        public void TrustedAccountKeyValue(string server, string expectedResult)
        {
            _serverConfig.TrustedAccountKey = server;

            Assert.AreEqual(_defaultConfig.TrustedAccountKey, expectedResult);
        }


        [Test]
        [TestCase(null, null)]
        [TestCase("AccountId", "AccountId")]
        public void AccountIdValue(string server, string expectedResult)
        {
            _serverConfig.AccountId = server;

            Assert.AreEqual(_defaultConfig.AccountId, expectedResult);
        }

        [Test]
        [TestCase(null, null)]
        [TestCase(1234, 1234)]
        public void SamplingTargetValue(int server, int expectedResult)
        {
            _serverConfig.SamplingTarget = server;

            Assert.AreEqual(_defaultConfig.SamplingTarget, expectedResult);
        }

        [Test]
        [TestCase(null, null)]
        [TestCase(1234, 1234)]
        public void SamplingTargetPeriodInSecondsValue(int server, int expectedResult)
        {
            _serverConfig.SamplingTargetPeriodInSeconds = server;

            Assert.AreEqual(_defaultConfig.SamplingTargetPeriodInSeconds, expectedResult);
        }

        #endregion Distributed Tracing

        #region Span Events

        [Test]
        public void SpanEventsEnabledIsTrueInLocalConfigByDefault()
        {
            Assert.IsTrue(_localConfig.spanEvents.enabled);
        }

        [TestCase(true, true, ExpectedResult = true)]
        [TestCase(true, false, ExpectedResult = false)]
        [TestCase(false, true, ExpectedResult = false)]
        [TestCase(false, false, ExpectedResult = false)]
        public bool SpanEventsEnabledHasCorrectValue(bool distributedTracingEnabled, bool spanEventsEnabled)
        {
            _localConfig.spanEvents.enabled = spanEventsEnabled;
            _localConfig.distributedTracing.enabled = distributedTracingEnabled;

            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return _defaultConfig.SpanEventsEnabled;
        }

        [Test]
        public void SpanEventsMaxSamplesStoredOverriddenByEventHarvestConfig()
        {
            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.SpanEventHarvestLimitKey, 10 } }
            };

            Assert.AreEqual(10, _defaultConfig.SpanEventsMaxSamplesStored);
        }

        [Test]
        public void SpanEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), _defaultConfig.SpanEventsHarvestCycle);

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.SpanEventHarvestLimitKey, 10 } }
            };
            Assert.AreEqual(TimeSpan.FromSeconds(5), _defaultConfig.SpanEventsHarvestCycle);
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void SpanEventsAttributesEnabled(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.spanEvents.attributes.enabled = localAttributes;
            Assert.AreEqual(expectedResult, _defaultConfig.SpanEventsAttributesEnabled);
        }

        [TestCase(true, true, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(false, false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(false, true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void SpanEventsAttributesInclude(bool highSecurity, bool localAttributesEnabled, string[] attributes, string[] expectedResult)
        {
            _localConfig.highSecurity.enabled = highSecurity;
            _localConfig.spanEvents.attributes.enabled = localAttributesEnabled;
            _localConfig.spanEvents.attributes.include = new List<string>(attributes);
            Assert.AreEqual(expectedResult.Length, _defaultConfig.SpanEventsAttributesInclude.Count());
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void SpanEventsAttributesIncludeClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.spanEvents.attributes.include = new List<string>(attributes);
            Assert.AreEqual(expectedResult.Length, _defaultConfig.SpanEventsAttributesInclude.Count());
        }

        [TestCase(new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void SpanEventsAttributesExclude(string[] attributes, string[] expectedResult)
        {
            _localConfig.spanEvents.attributes.exclude = new List<string>(attributes);
            Assert.AreEqual(expectedResult.Length, _defaultConfig.SpanEventsAttributesExclude.Count());
        }

        #endregion

        #region InfiniteTracing

        [TestCase(null, null, ExpectedResult = 100000)]
        [TestCase(null, 3824, ExpectedResult = 3824)]
        [TestCase("214", null, ExpectedResult = 214)]
        [TestCase("", 3824, ExpectedResult = 3824)]
        [TestCase("6534", 3824, ExpectedResult = 6534)]
        [TestCase("abc", 203, ExpectedResult = 203)]
        [TestCase("-3", null, ExpectedResult = -3)]
        [TestCase(null, -623, ExpectedResult = -623)]
        public int InfiniteTracing_SpanQueueSize(string envConfigValue, int? localConfigValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_QUEUE_SIZE")).Returns(envConfigValue);

            if (localConfigValue.HasValue)
            {
                _localConfig.infiniteTracing.span_events.queue_size = localConfigValue.Value;
            }

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingQueueSizeSpans;
        }


        [Test]
        public void InfiniteTracing_TraceObserver
        (
            [Values("envHost.com", "", null)] string envHost,
            [Values("443", "", null)] string envPort,
            [Values("False", "", null)] string envSsl,
            [Values("localHost.com", "", null)] string localHost,
            [Values("8080", "", null)] string localPort,
            [Values("False", "", null)] string localSsl
        )
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_HOST"))
                .Returns(envHost);

            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_PORT"))
                .Returns(envPort);

            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TRACE_OBSERVER_SSL"))
                .Returns(envSsl);

            _localConfig.infiniteTracing.trace_observer.host = localHost;
            _localConfig.infiniteTracing.trace_observer.port = localPort;
            if (localSsl != null)
            {
                _localConfig.appSettings.Add(new configurationAdd() { key = "InfiniteTracingTraceObserverSsl", value = localSsl });
            }

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            var expectedHost = envHost != null
                ? envHost
                : localHost;


            var expectedPort = envHost != null  //This should be Host != null
                ? envPort
                : localPort;

            var expectedSsl = envHost != null
                ? envSsl
                : localSsl;

            NrAssert.Multiple
            (
                () => Assert.AreEqual(expectedHost, defaultConfig.InfiniteTracingTraceObserverHost),
                () => Assert.AreEqual(expectedPort, defaultConfig.InfiniteTracingTraceObserverPort),
                () => Assert.AreEqual(expectedSsl, defaultConfig.InfiniteTracingTraceObserverSsl)
            );
        }

        [TestCase("12000", "232", ExpectedResult = 12000)]
        [TestCase("-342", "198", ExpectedResult = -342)]
        [TestCase(null, null, ExpectedResult = 10000)]
        [TestCase("", null, ExpectedResult = 10000)]
        [TestCase(null, "", ExpectedResult = 10000)]
        [TestCase("", "", ExpectedResult = 10000)]
        [TestCase("", "", ExpectedResult = 10000)]
        [TestCase("XYZ", "104", ExpectedResult = 104)]
        [TestCase("XYZ", "ABC", ExpectedResult = 10000)]
        public int InfiniteTracing_TimeoutData(string envConfigVal, string appSettingsValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingTimeoutSend", value = appSettingsValue });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TIMEOUT_SEND")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingTraceTimeoutMsSendData;
        }

        [TestCase("30000", "232", ExpectedResult = 30000)]
        [TestCase("-342", "198", ExpectedResult = -342)]
        [TestCase(null, null, ExpectedResult = 10000)]
        [TestCase("", null, ExpectedResult = 10000)]
        [TestCase(null, "", ExpectedResult = 10000)]
        [TestCase("", "", ExpectedResult = 10000)]
        [TestCase("XYZ", "104", ExpectedResult = 104)]
        [TestCase("XYZ", "ABC", ExpectedResult = 10000)]
        public int InfiniteTracing_TimeoutConnect(string envConfigVal, string appSettingsValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingTimeoutConnect", value = appSettingsValue });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_TIMEOUT_CONNECT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingTraceTimeoutMsConnect;
        }


        [TestCase("100", "232", ExpectedResult = 100f)]
        [TestCase("-342", "198", ExpectedResult = -342f)]
        [TestCase(null, null, ExpectedResult = null)]
        [TestCase("", null, ExpectedResult = null)]
        [TestCase(null, "", ExpectedResult = null)]
        [TestCase("", "", ExpectedResult = null)]
        [TestCase("", "203", ExpectedResult = 203f)]
        [TestCase("XYZ", "876", ExpectedResult = 876f)]
        [TestCase("XYZ", "ABC", ExpectedResult = null)]
        [TestCase("103.98", "100", ExpectedResult = 103.98f)]
        [TestCase(null, "98.6", ExpectedResult = 98.6f)]
        public float? InfiniteTracing_SpanTestFlaky(string envConfigVal, string appSettingsValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingSpanEventsTestFlaky", value = appSettingsValue });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_FLAKY")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingTraceObserverTestFlaky;
        }


        [TestCase("100", "232", ExpectedResult = 100)]
        [TestCase("-342", "198", ExpectedResult = -342)]
        [TestCase(null, null, ExpectedResult = 700)]
        [TestCase("", null, ExpectedResult = 700)]
        [TestCase(null, "", ExpectedResult = 700)]
        [TestCase("", "", ExpectedResult = 700)]
        [TestCase("", "203", ExpectedResult = 203)]
        [TestCase("XYZ", "876", ExpectedResult = 876)]
        [TestCase("XYZ", "ABC", ExpectedResult = 700)]
        [TestCase("103.98", null, ExpectedResult = 700)]
        [TestCase("103.98", "200", ExpectedResult = 200)]
        [TestCase(null, "98.6", ExpectedResult = 700)]
        public int InfiniteTracing_SpanBatchSize(string envConfigVal, string appSettingVal)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingSpanEventsBatchSize", value = appSettingVal });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_BATCH_SIZE")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingBatchSizeSpans;
        }


        [TestCase("100", "232", ExpectedResult = 100)]
        [TestCase("-342", "198", ExpectedResult = -342)]
        [TestCase(null, null, ExpectedResult = 62)]
        [TestCase("", null, ExpectedResult = 62)]
        [TestCase(null, "", ExpectedResult = 62)]
        [TestCase("", "", ExpectedResult = 62)]
        [TestCase("", "203", ExpectedResult = 203)]
        [TestCase("XYZ", "876", ExpectedResult = 876)]
        [TestCase("XYZ", "ABC", ExpectedResult = 62)]
        [TestCase("103.98", null, ExpectedResult = 62)]
        [TestCase("103.98", "200", ExpectedResult = 200)]
        [TestCase(null, "98.6", ExpectedResult = 62)]
        public int InfiniteTracing_SpanPartitionCount(string envConfigVal, string appSettingVal)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingSpanEventsPartitionCount", value = appSettingVal });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_PARTITION_COUNT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingPartitionCountSpans;
        }

        [TestCase("100", "232", ExpectedResult = 100)]
        [TestCase("-342", "198", ExpectedResult = -342)]
        [TestCase(null, null, ExpectedResult = null)]
        [TestCase("", null, ExpectedResult = null)]
        [TestCase(null, "", ExpectedResult = null)]
        [TestCase("", "", ExpectedResult = null)]
        [TestCase("", "203", ExpectedResult = 203)]
        [TestCase("XYZ", "876", ExpectedResult = 876)]
        [TestCase("XYZ", "ABC", ExpectedResult = null)]
        public int? InfiniteTracing_SpanTestDelay(string envConfigVal, string appSettingsValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingSpanEventsTestDelay", value = appSettingsValue });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_TEST_DELAY")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.InfiniteTracingTraceObserverTestDelayMs;
        }

        [Test]
        public void InfiniteTracing_SpanStreamsCount
        ([Values("10", "", "abc", null)] string envConfigVal,
            [Values("8", "", "def", null)] string appSettingsValue
        )
        {
            var expectedResult = 10;

            if (int.TryParse(envConfigVal, out var envValInt))
            {
                expectedResult = envValInt;
            }
            else if (int.TryParse(appSettingsValue, out var appValInt))
            {
                expectedResult = appValInt;
            }

            _localConfig.appSettings.Add(new configurationAdd { key = "InfiniteTracingSpanEventsStreamsCount", value = appSettingsValue });
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_STREAMS_COUNT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.AreEqual(expectedResult, defaultConfig.InfiniteTracingTraceCountConsumers);
        }



        #endregion

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
        public void UtilizationDetectKubernetesIsTrueByDefault()
        {
            Assert.IsTrue(_defaultConfig.UtilizationDetectKubernetes);
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
            Assert.AreEqual(expectedResult, _defaultConfig.TransactionEventsAttributesEnabled);
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

            Assert.AreEqual(expectedResult.Length, _defaultConfig.TransactionEventsAttributesInclude.Count());
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureTransactionEventAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.transactionEvents.attributes.include = new List<string>(attributes);

            Assert.AreEqual(expectedResult.Length, _defaultConfig.TransactionEventsAttributesInclude.Count());
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
        public void CaptureBrowserMonitoringAttributesIncludes(bool highSecurity, bool localAttributesEnabled, string[] attributes, string[] expectedResult)
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

        [Test]
        public void CustomEventsMaxSamplesStoredPassesThroughToLocalConfig()
        {
            Assert.That(_defaultConfig.CustomEventsMaximumSamplesStored, Is.EqualTo(10000));

            _localConfig.customEvents.maximumSamplesStored = 10001;
            Assert.That(_defaultConfig.CustomEventsMaximumSamplesStored, Is.EqualTo(10001));

            _localConfig.customEvents.maximumSamplesStored = 9999;
            Assert.That(_defaultConfig.CustomEventsMaximumSamplesStored, Is.EqualTo(9999));
        }

        [Test]
        public void CustomEventsMaxSamplesStoredOverriddenByEventHarvestConfig()
        {
            _localConfig.customEvents.maximumSamplesStored = 10001;
            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.CustomEventHarvestLimitKey, 10 } }
            };

            Assert.AreEqual(10, _defaultConfig.CustomEventsMaximumSamplesStored);
        }

        [TestCase("10", 20, 30, ExpectedResult = 10)]
        [TestCase("10", null, 30, ExpectedResult = 10)]
        [TestCase("10", 20, null, ExpectedResult = 10)]
        [TestCase("10", null, null, ExpectedResult = 10)]
        [TestCase(null, 20, 30, ExpectedResult = 30)]
        [TestCase(null, null, 30, ExpectedResult = 30)]
        [TestCase(null, 20, null, ExpectedResult = 20)]
        [TestCase(null, null, null, ExpectedResult = 10000)]
        public int CustomEventsMaxSamplesStoredOverriddenByEnvironment(string environmentSetting, int? localSetting, int? serverSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariable("MAX_EVENT_SAMPLES_STORED")).Returns(environmentSetting);

            if (localSetting != null)
            {
                _localConfig.customEvents.maximumSamplesStored = (int)localSetting;
            }

            if (serverSetting != null)
            {
                _serverConfig.EventHarvestConfig = new EventHarvestConfig
                {
                    ReportPeriodMs = 5000,
                    HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.CustomEventHarvestLimitKey, (int)serverSetting } }
                };
            }

            return _defaultConfig.CustomEventsMaximumSamplesStored;
        }


        [Test]
        public void CustomEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            Assert.AreEqual(TimeSpan.FromMinutes(1), _defaultConfig.CustomEventsHarvestCycle);

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.CustomEventHarvestLimitKey, 10 } }
            };
            Assert.AreEqual(TimeSpan.FromSeconds(5), _defaultConfig.CustomEventsHarvestCycle);
        }

        [Test]
        public void CustomEventsMaxSamplesOf0ShouldDisableCustomEvents()
        {
            _localConfig.customEvents.maximumSamplesStored = 0;
            Assert.IsFalse(_defaultConfig.CustomEventsEnabled);
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
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return _defaultConfig.SecurityPoliciesTokenExists;
        }

        #endregion SecurityPolicies

        [TestCase(null, ExpectedResult = false)]
        [TestCase("not a bool", ExpectedResult = false)]
        [TestCase("false", ExpectedResult = false)]
        [TestCase("true", ExpectedResult = true)]
        public bool AsyncHttpClientSegmentsDoNotCountTowardsParentExclusiveTimeTests(string localConfigValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "ForceSynchronousTimingCalculation.HttpClient", value = localConfigValue });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            return defaultConfig.ForceSynchronousTimingCalculationHttpClient;
        }

        private void CreateDefaultConfiguration()
        {
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
        }
    }
}
