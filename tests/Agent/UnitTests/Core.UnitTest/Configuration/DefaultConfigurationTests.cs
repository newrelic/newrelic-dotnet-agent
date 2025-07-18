// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.AgentHealth;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.DataTransport;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration.UnitTest
{
    internal class TestableDefaultConfiguration : DefaultConfiguration
    {
        public TestableDefaultConfiguration(IEnvironment environment, configuration localConfig, ServerConfiguration serverConfig, RunTimeConfiguration runTimeConfiguration, SecurityPoliciesConfiguration securityPoliciesConfiguration, IBootstrapConfiguration bootstrapConfiguration, IProcessStatic processStatic, IHttpRuntimeStatic httpRuntimeStatic, IConfigurationManagerStatic configurationManagerStatic, IDnsStatic dnsStatic, IAgentHealthReporter agentHealthReporter)
            : base(environment, localConfig, serverConfig, runTimeConfiguration, securityPoliciesConfiguration, bootstrapConfiguration, processStatic, httpRuntimeStatic, configurationManagerStatic, dnsStatic, agentHealthReporter)
        { }
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
        private IBootstrapConfiguration _bootstrapConfiguration;
        private DefaultConfiguration _defaultConfig;
        private IDnsStatic _dnsStatic;
        private IAgentHealthReporter _agentHealthReporter;

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
            _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
        }

        [TestCase(true, "something", true, "something")]
        [TestCase(false, "somethingelse", false, "somethingelse")]
        public void AgentEnabledShouldUseBootstrapConfig(bool enabled, string enabledAt, bool expectedEnabledValue, string expectedEnabledAtValue)
        {
            Mock.Arrange(() => _bootstrapConfiguration.AgentEnabled).Returns(enabled);
            Mock.Arrange(() => _bootstrapConfiguration.AgentEnabledAt).Returns(enabledAt);

            Assert.Multiple(() =>
            {
                Assert.That(_defaultConfig.AgentEnabled, Is.EqualTo(expectedEnabledValue));
                Assert.That(_defaultConfig.AgentEnabledAt, Is.EqualTo(expectedEnabledAtValue));
            });
        }

        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(true, true, ExpectedResult = true)]
        [TestCase(true, false, ExpectedResult = false)]
        [TestCase(false, true, ExpectedResult = false)]
        [TestCase(false, false, ExpectedResult = false)]
        public bool TransactionEventsCanBeDisabledByServer(bool? server, bool local)
        {
            _localConfig.transactionEvents.enabled = local;

            _serverConfig.AnalyticsEventCollectionEnabled = server;

            return _defaultConfig.TransactionEventsEnabled;
        }

        [Test]
        public void EveryConfigShouldGetNewVersionNumber()
        {
            var newConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(newConfig.ConfigurationVersion - 1, Is.EqualTo(_defaultConfig.ConfigurationVersion));
        }

        [Test]
        public void WhenConfigsAreDefaultThenTransactionEventsAreEnabled()
        {
            Assert.That(_defaultConfig.TransactionEventsEnabled, Is.True);
        }

        [Test]
        public void WhenConfigsAreDefaultThenPutForDataSendIsDisabled()
        {
            Assert.That(_defaultConfig.PutForDataSend, Is.False);
        }

        [Test]
        public void WhenConfigsAreDefaultThenInstanceReportingEnabledIsEnabled()
        {
            Assert.That(_defaultConfig.InstanceReportingEnabled, Is.True);
        }

        [Test]
        public void WhenConfigsAreDefaultThenDatabaseNameReportingEnabledIsEnabled()
        {
            Assert.That(_defaultConfig.DatabaseNameReportingEnabled, Is.True);
        }

        [Test]
        public void WhenConfigsAreDefaultThenDatastoreTracerQueryParametersEnabledIsDisabled()
        {
            Assert.That(_defaultConfig.DatastoreTracerQueryParametersEnabled, Is.False);
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
            Assert.That(_defaultConfig.CompressedContentEncoding, Is.EqualTo("deflate"));
        }

        [Test]
        public void WhenTransactionEventsAreEnabledInLocalConfigAndDoNotExistInServerConfigThenTransactionEventsAreEnabled()
        {
            _localConfig.transactionEvents.enabled = true;
            Assert.That(_defaultConfig.TransactionEventsEnabled, Is.True);
        }

        [Test]
        public void WhenConfigsAreDefaultThenCaptureAgentTimingIsDisabled()
        {
            Assert.That(_defaultConfig.DiagnosticsCaptureAgentTiming, Is.EqualTo(false));
        }

        [Test]
        public void WhenTransactionEventsAreDisabledInLocalConfigAndDoNotExistInServerConfigThenTransactionEventsAreDisabled()
        {
            _localConfig.transactionEvents.enabled = false;
            Assert.That(_defaultConfig.TransactionEventsEnabled, Is.False);
        }

        [Test]
        public void TransactionEventsMaxSamplesStoredPassesThroughToLocalConfig()
        {
            Assert.That(_defaultConfig.TransactionEventsMaximumSamplesStored, Is.EqualTo(10000));

            _localConfig.transactionEvents.maximumSamplesStored = 10001;
            Assert.That(_defaultConfig.TransactionEventsMaximumSamplesStored, Is.EqualTo(10001));

            _localConfig.transactionEvents.maximumSamplesStored = 9999;
            Assert.That(_defaultConfig.TransactionEventsMaximumSamplesStored, Is.EqualTo(9999));
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

            Assert.That(_defaultConfig.TransactionEventsMaximumSamplesStored, Is.EqualTo(10));
        }

        [TestCase("10", 20, 30, ExpectedResult = 30)]
        [TestCase("10", null, 30, ExpectedResult = 30)]
        [TestCase("10", 20, null, ExpectedResult = 10)]
        [TestCase("10", null, null, ExpectedResult = 10)]
        [TestCase(null, 20, 30, ExpectedResult = 30)]
        [TestCase(null, null, 30, ExpectedResult = 30)]
        [TestCase(null, 20, null, ExpectedResult = 20)]
        [TestCase(null, null, null, ExpectedResult = 10000)]
        public int TransactionEventsMaxSamplesStoredOverriddenByEnvironment(string environmentSetting, int? localSetting, int? serverSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("MAX_TRANSACTION_SAMPLES_STORED")).Returns(environmentSetting);

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
            Assert.That(_defaultConfig.TransactionEventsHarvestCycle, Is.EqualTo(TimeSpan.FromMinutes(1)));

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.TransactionEventHarvestLimitKey, 10 } }
            };
            Assert.That(_defaultConfig.TransactionEventsHarvestCycle, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void TransactionEventsMaxSamplesOf0ShouldDisableTransactionEvents()
        {
            _localConfig.transactionEvents.maximumSamplesStored = 0;
            Assert.That(_defaultConfig.TransactionEventsEnabled, Is.False);
        }

        [Test]
        public void DisableServerConfigIsFalseByDefault()
        {
            Assert.That(_defaultConfig.IgnoreServerSideConfiguration, Is.False);
        }

        [TestCase("true", ExpectedResult = true)]
        [TestCase("false", ExpectedResult = false)]
        public bool DisableServerConfigSetFromEnvironment(string environment)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_IGNORE_SERVER_SIDE_CONFIG")).Returns(environment);
            return _defaultConfig.IgnoreServerSideConfiguration;
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
            Assert.That(_defaultConfig.ErrorsMaximumPerPeriod, Is.EqualTo(20));
        }

        [Test]
        public void SqlTracesPerPeriodReturnsStatic10()
        {
            Assert.That(_defaultConfig.SqlTracesPerPeriod, Is.EqualTo(10));
        }

        [Test]
        public void SlowSqlServerOverridesWhenSet()
        {
            _serverConfig.RpmConfig.SlowSqlEnabled = true;
            _localConfig.slowSql.enabled = false;

            Assert.That(_defaultConfig.SlowSqlEnabled, Is.EqualTo(true));
        }

        [Test]
        public void SlowSqlServerOverridesWhenLocalIsDefault()
        {
            _serverConfig.RpmConfig.SlowSqlEnabled = false;

            Assert.That(_defaultConfig.SlowSqlEnabled, Is.EqualTo(false));
        }

        [Test]
        public void SlowSqlDefaultIsTrue()
        {
            Assert.That(_defaultConfig.SlowSqlEnabled, Is.True);
        }

        [Test]
        public void SlowSqlLocalConfigSetToFalse()
        {
            _localConfig.slowSql.enabled = false;
            Assert.That(_defaultConfig.SlowSqlEnabled, Is.False);
        }

        [Test]
        public void WhenStackTraceMaximumFramesIsSet()
        {
            _localConfig.maxStackTraceLines = 100;
            Assert.That(_defaultConfig.StackTraceMaximumFrames, Is.EqualTo(100));
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

        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("1", false, ExpectedResult = true)]
        [TestCase("0", true, ExpectedResult = false)]
        [TestCase("blarg", true, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        public bool SendDataOnExitIsOverriddenByEnvironment(string environmentSetting, bool localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_SEND_DATA_ON_EXIT")).Returns(environmentSetting);
            _localConfig.service.sendDataOnExit = localSetting;
            return _defaultConfig.CollectorSendDataOnExit;
        }

        [TestCase("100", 500f, ExpectedResult = 100f)]
        [TestCase("blarg", 500f, ExpectedResult = 500f)]
        [TestCase(null, 500f, ExpectedResult = 500f)]
        public float SendDataOnExitThresholdIsOverriddenByEnvironment(string environmentSetting, float localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_SEND_DATA_ON_EXIT_THRESHOLD_MS")).Returns(environmentSetting);
            _localConfig.service.sendDataOnExitThreshold = localSetting;
            return _defaultConfig.CollectorSendDataOnExitThreshold;
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
                () => Assert.That(_defaultConfig.DiagnosticsCaptureAgentTiming, Is.EqualTo(expectedIsEnabled), "Performance Timing Enabled"),
                () => Assert.That(_defaultConfig.DiagnosticsCaptureAgentTimingFrequency, Is.EqualTo(expectedFrequency), "Perforamcne Timing Frequency")
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

            Assert.That(_defaultConfig.ErrorCollectorMaxEventSamplesStored, Is.EqualTo(10));
        }

        [Test]
        public void ErrorEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            Assert.That(_defaultConfig.ErrorEventsHarvestCycle, Is.EqualTo(TimeSpan.FromMinutes(1)));

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.ErrorEventHarvestLimitKey, 10 } }
            };
            Assert.That(_defaultConfig.ErrorEventsHarvestCycle, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void ErrorEventsMaxSamplesOf0ShouldDisableErrorEvents()
        {
            _localConfig.errorCollector.maxEventSamplesStored = 0;
            Assert.That(_defaultConfig.ErrorCollectorCaptureEvents, Is.False);
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
#if NET
        [TestCase(3000.5, null, ExpectedResult = 3000.5)] // .NET doesn't round timespans the same way Framework did...
#else
        [TestCase(3000.5, null, ExpectedResult = 3001.0)]
#endif
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
            Assert.That(_defaultConfig.SqlStatementsPerTransaction, Is.EqualTo(500));
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

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "1", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(true, "0", ExpectedResult = false)]
        [TestCase(true, "invalid", ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        [TestCase(false, "1", ExpectedResult = true)]
        [TestCase(false, "false", ExpectedResult = false)]
        [TestCase(false, "0", ExpectedResult = false)]
        [TestCase(false, "invalid", ExpectedResult = false)]
        [TestCase(null, "true", ExpectedResult = true)]
        [TestCase(null, "1", ExpectedResult = true)]
        [TestCase(null, "false", ExpectedResult = false)]
        [TestCase(null, "0", ExpectedResult = false)]
        [TestCase(null, "invalid", ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = false)]
        public bool HighSecuritySetFromEnvironmentOverridesLocal(bool? localConfigValue, string envConfigValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_HIGH_SECURITY")).Returns(envConfigValue);

            if (localConfigValue.HasValue)
            {
                _localConfig.highSecurity.enabled = localConfigValue.Value;
            }

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
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
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

            Assert.That(_defaultConfig.BrowserMonitoringUseSsl, Is.True);
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



        [TestCase("apdex_f", null, 5, ExpectedResult = 20000)]
        [TestCase("1", null, 5, ExpectedResult = 1)]
#if NETFRAMEWORK
        [TestCase("1.5", null, 5, ExpectedResult = 2)]
#else
        [TestCase("1.5", null, 5, ExpectedResult = 1.5)]
#endif
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

            Assert.That(_defaultConfig.TransactionTraceThreshold.TotalSeconds, Is.EqualTo(42 * 4));
        }

        [Test]
        public void ApdexT_SetFromEnvironmentVariable_WhenInServerlessMode()
        {
            // set NEW_RELIC_APDEX_T environment variable
                Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_APDEX_T")).Returns("1.234");

            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);

            Assert.That(_defaultConfig.TransactionTraceApdexT, Is.EqualTo(TimeSpan.FromSeconds(1.234)));
        }

        [Test]
        public void CaptureCustomParametersSetFromLocalDefaultsToTrue()
        {
            Assert.That(_defaultConfig.CaptureCustomParameters, Is.True);
        }

        [TestCase(false, false, false, ExpectedResult = true)]
        [TestCase(false, true, false, ExpectedResult = false)]
        [TestCase(false, true, true, ExpectedResult = true)]
        [TestCase(true, false, true, ExpectedResult = false)]
        [TestCase(true, true, true, ExpectedResult = false)]
        public bool CaptureCustomParametersHsmAndLocal(bool highSecurity, bool customParametersSpecified, bool customParametersEnabled)
        {
            _localConfig.highSecurity.enabled = highSecurity;

            if (customParametersSpecified)
            {
                _localConfig.customParameters.enabledSpecified = customParametersSpecified;
                _localConfig.customParameters.enabled = customParametersEnabled;
            }

            return _defaultConfig.CaptureCustomParameters;
        }

        [Test]
        public void CaptureIdentityParametersSetFromLocalDefaultsToFalse()
        {
            Assert.That(_defaultConfig.CaptureAttributesDefaultExcludes, Does.Contain("identity.*"));
        }

        [Test]
        public void CaptureResponseHeaderParametersSetFromLocalDefaultsToTrue()
        {
            Assert.That(_defaultConfig.CaptureAttributesExcludes, Does.Not.Contain("response.headers.*"));
        }

        [TestCase(new[] { "local" }, new[] { "server" }, ExpectedResult = "server")]
        [TestCase(new[] { "local" }, null, ExpectedResult = "local")]
        public string ExceptionsToIgnoreSetFromLocalAndServerOverrides(string[] local, string[] server)
        {
            _serverConfig.RpmConfig.ErrorCollectorIgnoreClasses = server;
            _localConfig.errorCollector.ignoreClasses.errorClass = new List<string>(local);

            CreateDefaultConfiguration();

            return _defaultConfig.IgnoreErrorsConfiguration.Keys.FirstOrDefault();
        }

        [Test]
        public void Decodes_IgnoreAndExpectedClasses_IgnoreAndExpectedMessages_ExpectedStatusCodes_Configurations_Successfully()
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

            _defaultConfig = new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(new[] { "404", "500" }, Is.EquivalentTo(_defaultConfig.ExpectedErrorStatusCodesForAgentSettings));

            var expectedMessages = _defaultConfig.ExpectedErrorsConfiguration;

            Assert.That(expectedMessages.ContainsKey("ErrorClass1"));

            var expectedErrorClass2 = expectedMessages.Where(x => x.Key == "ErrorClass2").FirstOrDefault();
            Assert.That(expectedErrorClass2.Value.Any(), Is.False);

            var expectedErrorClass3 = expectedMessages.Where(x => x.Key == "ErrorClass3").FirstOrDefault();
            Assert.That(expectedErrorClass3.Value, Does.Contain("error message 1 in ErrorClass3"));
            Assert.That(expectedErrorClass3.Value, Does.Contain("error message 2 in ErrorClass3"));

            var ignoreMessages = _defaultConfig.IgnoreErrorsConfiguration;

            Assert.That(ignoreMessages.ContainsKey("ErrorClass1"));

            var ignoreErrorClass2 = ignoreMessages.Where(x => x.Key == "ErrorClass2").FirstOrDefault();
            Assert.That(ignoreErrorClass2.Value.Any(), Is.False);

            var ignoreErrorClass3 = ignoreMessages.Where(x => x.Key == "ErrorClass3").FirstOrDefault();
            Assert.That(ignoreErrorClass3.Value, Does.Contain("error message 1 in ErrorClass3"));
            Assert.Multiple(() =>
            {
                Assert.That(ignoreErrorClass3.Value, Does.Contain("error message 2 in ErrorClass3"));

                Assert.That(_defaultConfig.IgnoreErrorsConfiguration.ContainsKey("404"));
                Assert.That(_defaultConfig.IgnoreErrorsConfiguration.ContainsKey("500"));
            });
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
        [TestCase(new[] { "Class1" }, new[] { "Class2" }, ExpectedResult = "Class1")]
        public string IgnoreErrorsAndIgnoreClassesCombineTests(string[] ignoreClasses, string[] ignoreErrors)
        {
            _localConfig.errorCollector.ignoreClasses.errorClass = new List<string>(ignoreClasses);
            _localConfig.errorCollector.ignoreErrors.exception = new List<string>(ignoreErrors);

            CreateDefaultConfiguration();
            return string.Join(",", _defaultConfig.IgnoreErrorsConfiguration.Keys);
        }

        [TestCase("401", new[] { "405" }, null, ExpectedResult = new[] { "405" })]
        [TestCase("401", new string[0], null, ExpectedResult = new string[0])]
        [TestCase("401", null, null, ExpectedResult = new[] { "401" })]
        [TestCase(null, null, "401", ExpectedResult = new[] { "401" })]
        [TestCase(null, new[] { "405" }, "401", ExpectedResult = new[] { "401" })]
        [TestCase("402", new string[0], "401", ExpectedResult = new[] { "401" })]
        [TestCase("402", new string[0], "401, 503", ExpectedResult = new[] { "401", "503" })]
        [TestCase("402", new string[0], "401, 500-505", ExpectedResult = new[] { "401", "500-505" })]
        public string[] ExpectedStatusCodesSetFromLocalServerAndEnvironmentOverrides(string local, string[] server, string env)
        {
            _serverConfig.RpmConfig.ErrorCollectorExpectedStatusCodes = server;
            _localConfig.errorCollector.expectedStatusCodes = (local);
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_ERROR_COLLECTOR_EXPECTED_ERROR_CODES")).Returns(env);

            CreateDefaultConfiguration();

            return _defaultConfig.ExpectedErrorStatusCodesForAgentSettings.ToArray();
        }

        [TestCase(new[] { 401f }, new[] { "405" }, null, ExpectedResult = new[] { "405" })]
        [TestCase(new[] { 401f }, new string[0], null, ExpectedResult = new string[0])]
        [TestCase(new[] { 401f }, null, null, ExpectedResult = new[] { "401" })]
        [TestCase(new[] { 401.5f }, null, null, ExpectedResult = new[] { "401.5" })]
        [TestCase(new float[0], null, "401", ExpectedResult = new[] { "401" })]
        [TestCase(new float[0], new[] { "405" }, "401", ExpectedResult = new[] { "401" })]
        [TestCase(new[] { 401f }, new string[0], "402", ExpectedResult = new[] { "402" })]
        [TestCase(new[] { 401f }, new string[0], "401.5, 503", ExpectedResult = new[] { "401.5", "503" })]
        public string[] IgnoredStatusCodesSetFromLocalServerAndEnvironmentOverrides(float[] local, string[] server, string env)
        {
            _serverConfig.RpmConfig.ErrorCollectorStatusCodesToIgnore = server;
            _localConfig.errorCollector.ignoreStatusCodes.code = (local.ToList());
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_ERROR_COLLECTOR_IGNORE_ERROR_CODES")).Returns(env);

            CreateDefaultConfiguration();

            return _defaultConfig.HttpStatusCodesToIgnore.ToArray();
        }

        [TestCase("401-404", new string[] { "401.5", "402.3" }, new bool[] { false, false })] //does not support full status codes
        [TestCase("400,401,404", new string[] { "400", "401", "402", "403", "404" }, new bool[] { true, true, false, false, true })]
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

            Assert.That(actual, Is.EqualTo(expected).AsCollection);

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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_CONFIG_OBSCURING_KEY")).Returns(envObscuringKey);

            _localConfig.service.proxy.password = password;
            _localConfig.service.obscuringKey = localConfigObscuringKey;
            _localConfig.service.proxy.passwordObfuscated = passwordObfuscated;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyPassword;
        }

        [TestCase("localtestvalue", "", ExpectedResult = "localtestvalue")]
        [TestCase("", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "   ", ExpectedResult = "localtestvalue")]
        [TestCase("   ", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("", "", ExpectedResult = "")]
        public string ProxyHost_Tests(string localProxyHost, string envProxyHost)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_HOST")).Returns(envProxyHost);

            _localConfig.service.proxy.host = localProxyHost;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyHost;
        }

        [TestCase("localtestvalue", "", ExpectedResult = "localtestvalue")]
        [TestCase("", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "   ", ExpectedResult = "localtestvalue")]
        [TestCase("   ", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("", "", ExpectedResult = "")]
        public string ProxyUriPath_Tests(string localProxyUriPath, string envProxyUriPath)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_URI_PATH")).Returns(envProxyUriPath);

            _localConfig.service.proxy.uriPath = localProxyUriPath;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyUriPath;
        }

        [TestCase(1234, "", ExpectedResult = 1234)]
        [TestCase(1234, "4321", ExpectedResult = 4321)]
        [TestCase(1234, "bob", ExpectedResult = 1234)]
        public int ProxyPort_Tests(int localProxyPort, string envProxyPort)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_PORT")).Returns(envProxyPort);

            _localConfig.service.proxy.port = localProxyPort;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyPort;
        }

        [TestCase("localtestvalue", "", ExpectedResult = "localtestvalue")]
        [TestCase("", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "   ", ExpectedResult = "localtestvalue")]
        [TestCase("   ", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("", "", ExpectedResult = "")]
        public string ProxyUsername_Tests(string localProxyUser, string envProxyUser)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_USER")).Returns(envProxyUser);

            _localConfig.service.proxy.user = localProxyUser;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyUsername;
        }

        [TestCase("localtestvalue", "", ExpectedResult = "localtestvalue")]
        [TestCase("", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "   ", ExpectedResult = "localtestvalue")]
        [TestCase("   ", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("", "", ExpectedResult = "")]
        public string ProxyPassword_Tests(string localProxyPassword, string envProxyPassword)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_PASS")).Returns(envProxyPassword);

            _localConfig.service.proxy.password = localProxyPassword;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyPassword;
        }

        [TestCase("localtestvalue", "", ExpectedResult = "localtestvalue")]
        [TestCase("", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("localtestvalue", "   ", ExpectedResult = "localtestvalue")]
        [TestCase("   ", "envtestvalue", ExpectedResult = "envtestvalue")]
        [TestCase("", "", ExpectedResult = "")]
        public string ProxyDomain_Tests(string localProxyDomain, string envProxyDomain)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROXY_DOMAIN")).Returns(envProxyDomain);

            _localConfig.service.proxy.domain = localProxyDomain;

            CreateDefaultConfiguration();

            return _defaultConfig.ProxyDomain;
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
                () => Assert.That(_defaultConfig.ExpectedErrorStatusCodesForAgentSettings, Is.EquivalentTo(expectedStatusCodes)),
                () => Assert.That(_defaultConfig.ExpectedErrorClassesForAgentSettings, Is.EquivalentTo(expectedErrorClasses)),
                () => Assert.That(_defaultConfig.ExpectedErrorMessagesForAgentSettings, Is.EquivalentTo(expectedErrorMessages))
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
            Assert.That(DefaultConfiguration.Instance, Is.Not.Null);
        }

        [Test]
        public void StaticRequestPathExclusionListIsNotNull()
        {
            Assert.That(DefaultConfiguration.Instance.RequestPathExclusionList, Is.Not.Null);
        }

        [Test]
        public void RequestPathExclusionListIsEmptyIfNoRequestPathsInList()
        {
            Assert.That(DefaultConfiguration.Instance.RequestPathExclusionList, Is.Empty);
        }

        [Test]
        public void RequestPathExclusionListContainsOneEntry()
        {
            var path = new configurationBrowserMonitoringPath();
            path.regex = "one";

            _localConfig.browserMonitoring.requestPathsExcluded.Add(path);

            Assert.That(_defaultConfig.RequestPathExclusionList.Count(), Is.EqualTo(1));
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

            Assert.That(_defaultConfig.RequestPathExclusionList.Count(), Is.EqualTo(2));
        }

        [Test]
        public void RequestPathExclusionListBadRegex()
        {
            var path = new configurationBrowserMonitoringPath();
            path.regex = ".*(?<!\\)\\(?!\\).*";

            _localConfig.browserMonitoring.requestPathsExcluded.Add(path);

            Assert.That(_defaultConfig.RequestPathExclusionList.Count(), Is.EqualTo(0));
        }

        [Test]
        public void BrowserMonitoringJavaScriptAgentLoaderTypeSetToDefaultRum()
        {
            Assert.That(_defaultConfig.BrowserMonitoringJavaScriptAgentLoaderType, Is.EqualTo("rum"));
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

            _defaultConfig = new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(_defaultConfig.ThreadProfilingIgnoreMethods, Does.Contain("System.Threading.WaitHandle:WaitAny"));
        }

        [Test]
        public void BrowserMonitoringUsesDefaultWhenNoConfigValue()
        {
            _localConfig.browserMonitoring.attributes.enabledSpecified = false;

            Assert.That(_defaultConfig.CaptureBrowserMonitoringAttributes, Is.False);
        }

        [Test]
        public void ErrorCollectorUsesDefaultWhenNoConfigValue()
        {
            _localConfig.errorCollector.attributes.enabledSpecified = false;

            Assert.That(_defaultConfig.CaptureErrorCollectorAttributes, Is.True);
        }

        [Test]
        public void TransactionTracerUsesDefaultWhenNoConfigValue()
        {
            _localConfig.transactionTracer.attributes.enabledSpecified = false;

            Assert.That(_defaultConfig.CaptureTransactionTraceAttributes, Is.True);
        }

        [Test]
        public void TransactionEventUsesDefaultWhenNoConfigValues()
        {
            Assert.That(_defaultConfig.TransactionEventsAttributesEnabled, Is.True);
        }

        [TestCase(true, true, ExpectedResult = true)]
        [TestCase(true, false, ExpectedResult = true)]
        [TestCase(false, true, ExpectedResult = true)]
        [TestCase(false, false, ExpectedResult = false)]
        public bool CompleteTransactionsOnThreadConfig(
            bool completeTransactionsOnThread,
            bool serverlessModeEnabled)
        {
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(serverlessModeEnabled);

            _localConfig.service.completeTransactionsOnThread = completeTransactionsOnThread;

            return _defaultConfig.CompleteTransactionsOnThread;
        }

        [TestCase(null, null, null, ExpectedResult = null)]
        [TestCase(null, null, "Foo", ExpectedResult = "Foo")]
        [TestCase(null, "Foo", null, ExpectedResult = "Foo")]
        [TestCase(null, "Foo", "Bar", ExpectedResult = "Foo")]
        [TestCase("appConfigValue", null, null, ExpectedResult = "appConfigValue")]
        [TestCase("appConfigValue", null, "Foo", ExpectedResult = "appConfigValue")]
        [TestCase("appConfigValue", "Foo", null, ExpectedResult = "appConfigValue")]
        [TestCase("appConfigValue", "Foo", "Bar", ExpectedResult = "appConfigValue")]
        public string LabelsAreOverriddenProperlyAndAreCached(string appConfigValue, string environment, string local)
        {
            _localConfig.labels = local;
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsLabels)).Returns(appConfigValue);
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_LABELS")).Returns(environment);

            // call Labels accessor multiple times to verify caching behavior
            string result = _defaultConfig.Labels;
            for (var i = 0; i < 10; ++i)
            {
                result = _defaultConfig.Labels;
            }

            // Checking that the underlying abstractions are only ever called once verifies caching behavior
            Mock.Assert(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsLabels), Occurs.AtMost(1));
            Mock.Assert(() => _environment.GetEnvironmentVariable("NEW_RELIC_LABELS"), Occurs.AtMost(1));

            return result;
        }

        [TestCase(null, null, ExpectedResult = null)]
        [TestCase(null, "Foo", ExpectedResult = "Foo")]
        [TestCase("Foo", null, ExpectedResult = "Foo")]
        [TestCase("Foo", "Bar", ExpectedResult = "Foo")]
        public string CustomHostEnvironmentOverridesLocal(string environment, string local)
        {
            _localConfig.service.host = local;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_HOST")).Returns(environment);

            return _defaultConfig.CollectorHost;
        }

        // all null returns empty string
        [TestCase(null, null, null, ExpectedResult = "")]
        // AppSetting overrides environment and local
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0", null, null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0", null, "bar1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0", "bar1234567890abcdefghijklmnopqrstuvwxyz0", null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0", "bar1234567890abcdefghijklmnopqrstuvwxyz0", "nar1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        // Environment overrides local
        [TestCase(null, "foo1234567890abcdefghijklmnopqrstuvwxyz0", null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase(null, "foo1234567890abcdefghijklmnopqrstuvwxyz0", "bar1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        // local on its own
        [TestCase(null, null, "foo1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase(null, null, "REPLACE_WITH_LICENSE_KEY", ExpectedResult = "REPLACE_WITH_LICENSE_KEY")]
        // Length must be 40
        [TestCase("       foo1234567890abcdefghijklmnopqrstuvwxyz0         ", null, null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0123456789", null, null, ExpectedResult = "")]
        [TestCase("foo", null, null, ExpectedResult = "")]
        // Allowed characters
        [TestCase("foo1234567890abcdefghijklmnopqrstuvyz\tzz", null, null, ExpectedResult = "")]
        // Bad keys skipped for lower priority keys
        [TestCase("foo1234567890abcdefghijklmnopqrstuvwxyz0123456789", "foo1234567890abcdefghijklmnopqrstuvwxyz0", null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase(null, "foo1234567890abcdefghijklmnopqrstuvwxyz0123456789", "foo1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo", "foo1234567890abcdefghijklmnopqrstuvwxyz0", null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase(null, "foo", "foo1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase("foo1234567890abcdefghijklmnopqrstuvyz\tzz", "foo1234567890abcdefghijklmnopqrstuvwxyz0", null, ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        [TestCase(null, "foo1234567890abcdefghijklmnopqrstuvyz\tzz", "foo1234567890abcdefghijklmnopqrstuvwxyz0", ExpectedResult = "foo1234567890abcdefghijklmnopqrstuvwxyz0")]
        public string LicenseKeyEnvironmentOverridesLocal(string appSettingEnvironmentName, string newEnvironmentName, string local)
        {
            _localConfig.service.licenseKey = local;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_LICENSE_KEY", "NEWRELIC_LICENSEKEY")).Returns(newEnvironmentName);
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsLicenseKey)).Returns(appSettingEnvironmentName);

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

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_SPAN_EVENTS_ENABLED")).Returns(environmentSpanEvents);

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

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_ENABLED")).Returns(environmentDistributedTracing);

            return _defaultConfig.DistributedTracingEnabled;
        }

        [TestCase(null, ExpectedResult = false)]
        [TestCase("invalidValue", ExpectedResult = false)]
        [TestCase("False", ExpectedResult = false)]
        [TestCase("false", ExpectedResult = false)]
        [TestCase("0", ExpectedResult = false)]
        [TestCase("1", ExpectedResult = true)]
        [TestCase("True", ExpectedResult = true)]
        [TestCase("true", ExpectedResult = true)]
        public bool AppDomainCachingDisabledWorksAsExpected(string environmentAppDomainCachingDisabled)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISABLE_APPDOMAIN_CACHING")).Returns(environmentAppDomainCachingDisabled);

            return _defaultConfig.AppDomainCachingDisabled;
        }

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        public bool DisableSamplersEnvironmentOverridesLocal(bool localDisableSamplers, string environmentDisableSamplers)
        {
            _localConfig.application.disableSamplers = localDisableSamplers;

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISABLE_SAMPLERS")).Returns(environmentDisableSamplers);

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
                () => Assert.That(_defaultConfig.UrlRegexRules.Count(), Is.EqualTo(2)),

                // Rule 1
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).MatchExpression, Is.EqualTo("apple")),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).Replacement, Is.EqualTo("banana")),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).EachSegment, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).EvaluationOrder, Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).Ignore, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).ReplaceAll, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).TerminateChain, Is.EqualTo(true)),

                // Rule 2
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).MatchExpression, Is.EqualTo("pie")),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).Replacement, Is.EqualTo(null)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).EachSegment, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).EvaluationOrder, Is.EqualTo(0)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).Ignore, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).ReplaceAll, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.UrlRegexRules.ElementAt(1).TerminateChain, Is.EqualTo(false))
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

            Assert.That(_defaultConfig.UrlRegexRules.ElementAt(0).Replacement, Is.EqualTo(expectedOutput));
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
                () => Assert.That(_defaultConfig.MetricNameRegexRules.Count(), Is.EqualTo(2)),

                // Rule 1
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).MatchExpression, Is.EqualTo("apple")),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).Replacement, Is.EqualTo("banana")),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).EachSegment, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).EvaluationOrder, Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).Ignore, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).ReplaceAll, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).TerminateChain, Is.EqualTo(true)),

                // Rule 2
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).MatchExpression, Is.EqualTo("pie")),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).Replacement, Is.EqualTo(null)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).EachSegment, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).EvaluationOrder, Is.EqualTo(0)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).Ignore, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).ReplaceAll, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(1).TerminateChain, Is.EqualTo(false))
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

            Assert.That(_defaultConfig.MetricNameRegexRules.ElementAt(0).Replacement, Is.EqualTo(expectedOutput));
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
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.Count(), Is.EqualTo(2)),

                // Rule 1
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).MatchExpression, Is.EqualTo("apple")),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).Replacement, Is.EqualTo("banana")),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).EachSegment, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).EvaluationOrder, Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).Ignore, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).ReplaceAll, Is.EqualTo(true)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).TerminateChain, Is.EqualTo(true)),

                // Rule 2
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).MatchExpression, Is.EqualTo("pie")),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).Replacement, Is.EqualTo(null)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).EachSegment, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).EvaluationOrder, Is.EqualTo(0)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).Ignore, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).ReplaceAll, Is.EqualTo(false)),
                () => Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(1).TerminateChain, Is.EqualTo(false))
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

            Assert.That(_defaultConfig.TransactionNameRegexRules.ElementAt(0).Replacement, Is.EqualTo(expectedOutput));
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
                () => Assert.That(_defaultConfig.WebTransactionsApdex, Has.Count.EqualTo(2)),
                () => Assert.That(_defaultConfig.WebTransactionsApdex.ContainsKey("apple"), Is.True),
                () => Assert.That(_defaultConfig.WebTransactionsApdex["apple"], Is.EqualTo(0.2)),
                () => Assert.That(_defaultConfig.WebTransactionsApdex.ContainsKey("banana"), Is.True),
                () => Assert.That(_defaultConfig.WebTransactionsApdex["banana"], Is.EqualTo(0.1))
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
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules.Count(), Is.EqualTo(2)),

                // Rule 1
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules.ContainsKey("apple/banana"), Is.True),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["apple/banana"], Is.Not.Null),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["apple/banana"].Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["apple/banana"].ElementAt(0), Is.EqualTo("pie")),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["apple/banana"].ElementAt(1), Is.EqualTo("cake")),

                // Rule 2
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules.ContainsKey("mango/peach"), Is.True),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["mango/peach"], Is.Not.Null),
                () => Assert.That(_defaultConfig.TransactionNameWhitelistRules["mango/peach"].Count(), Is.EqualTo(0))
                );
        }

        [TestCase(null, null, ExpectedResult = "coconut")]
        [TestCase("blandAmericanCoconut", null, ExpectedResult = "blandAmericanCoconut")]
        [TestCase("blandAmericanCoconut", "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        [TestCase(null, "vietnameseCoconut", ExpectedResult = "vietnameseCoconut")]
        public string ProcessHostDisplayNameIsSetFromLocalConfigurationAndEnvironmentVariable(string localConfigurationValue, string environmentVariableValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PROCESS_HOST_DISPLAY_NAME")).Returns(environmentVariableValue);
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

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string>();

            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns((string)null);

            Assert.Throws<Exception>(() => _defaultConfig.ApplicationNames.Any());
        }

        [Test]
        public void ApplicationNamesPullsNamesFromRuntimeConfig()
        {
            _runTimeConfig.ApplicationNames = new List<string> { "MyAppName1", "MyAppName2" };
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns((string)null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns((string)null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns((string)null);
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("API"))
            );
        }

        [Test]
        public void ApplicationNamesPullsSingleNameFromAppSettings()
        {
            _runTimeConfig.ApplicationNames = new List<string>();
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns("MyAppName");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Application Config"))
            );
        }

        [Test]
        public void ApplicationNamesPullsMultipleNamesFromAppSettings()
        {
            _runTimeConfig.ApplicationNames = new List<string>();
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns("MyAppName1,MyAppName2");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("OtherAppName");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Application Config"))
            );
        }

        [Test]
        public void ApplicationNamesPullsSingleNameFromIisExpressSitenameEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns((string)null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (IISEXPRESS_SITENAME)"))
            );
        }

        [Test]
        public void ApplicationNamesPullsMultipleNamesFromIisExpressSitenameEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();
            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns((string)null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("IISEXPRESS_SITENAME")).Returns("MyAppName1,MyAppName2");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("OtherAppName");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (IISEXPRESS_SITENAME)"))
            );
        }

        [Test]
        public void ApplicationNamesPullsSingleNameFromRoleNameEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("MyAppName");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (RoleName)"))
            );
        }

        [Test]
        public void ApplicationNamesPullsMultipleNamesFromRoleNameEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            Mock.Arrange(() => _environment.GetEnvironmentVariable("RoleName")).Returns("MyAppName1,MyAppName2");
            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (RoleName)"))
            );
        }

        [Test]
        public void ApplicationNamesPullsNamesFromNewRelicConfig()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "MyAppName1", "MyAppName2" };
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("OtherAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("NewRelic Config"))
            );
        }

        [Test]
        public void ApplicationNamesPullsSingleNameFromAppPoolIdEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns((string)null);

            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("MyAppName");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Application Pool"))
            );
        }

        [Test]
        public void ApplicationNamesPullsMultipleNamesFromAppPoolIdEnvironmentVariable()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("APP_POOL_ID", "ASPNETCORE_IIS_APP_POOL_ID")).Returns("MyAppName1,MyAppName2");
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("OtherAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(2)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName1")),
                () => Assert.That(_defaultConfig.ApplicationNames.ElementAtOrDefault(1), Is.EqualTo("MyAppName2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Application Pool"))
            );
        }

        [TestCase("AppPoolId", "w3wp.exe -ap AppPoolId", "Application Pool")]
        [TestCase("AppPoolId", "w3wp.exe -ap \"AppPoolId\"", "Application Pool")]
        [TestCase("W3WP", "w3wp.exe -app \"NotAnAppPool\"", "Process Name")]
        [TestCase("W3WP", "w3wp.exe -ap", "Process Name")]
        [TestCase("W3WP", "w3wp.exe -ap ", "Process Name")]
        [TestCase("AppPoolId", "w3wp.exe -firstArg -ap \"AppPoolId\" -thirdArg", "Application Pool")]
        public void ApplicationNamesPullsSingleNameFromAppPoolIdFromCommandLine(string expected, string commandLine, string expectedSource)
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string>();
            Mock.Arrange(() => _environment.GetCommandLineArgs()).Returns(commandLine.Split(new[] { ' ' }));
            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns<string>(null);
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("W3WP");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo(expected)),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo(expectedSource))
            );
        }

        [Test]
        public void ApplicationNamesPullsNameFromProcessIdIfAppDomainAppVirtualPathIsNull()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string>();

            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns<string>(null);
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns("MyAppName");

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyAppName")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Process Name"))
            );
        }

        [Test]
        public void ApplicationNamesUsesLambdaFunctionNameIfBlank()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessFunctionName).Returns("MyFunc");
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessFunctionVersion).Returns("2");
            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string>();

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyFunc")),
                () => Assert.That(_defaultConfig.ServerlessFunctionVersion, Is.EqualTo("2")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (AWS_LAMBDA_FUNCTION_NAME)"))
            );
        }

        [Test]
        public void ApplicationNamesUsesLambdaFunctionNameIfDefault()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessFunctionName).Returns("MyFunc");
            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "My Application" };

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("MyFunc")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (AWS_LAMBDA_FUNCTION_NAME)"))
            );
        }

        [Test]
        public void ApplicationNamesDoesNotUseLambdaFunctionNameIfEnvVarSet()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessFunctionName).Returns("MyFunc");
            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("NEW_RELIC_APP_NAME")).Returns("My App Name");

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "My Application" };

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("My App Name")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("Environment Variable (NEW_RELIC_APP_NAME)"))
            );
        }

        [Test]
        public void ApplicationNamesDoesNotUseLambdaFunctionNameIfBlank()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessFunctionName).Returns("");
            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "My Application" };

            NrAssert.Multiple(
                () => Assert.That(_defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(_defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("My Application")),
                () => Assert.That(_defaultConfig.ApplicationNamesSource, Is.EqualTo("NewRelic Config"))
            );
        }

        [Test]
        [TestCase(false, "My Application", "NewRelic Config")]
        [TestCase(true, "MyAzureFunc", "Azure Function")]
        public void ApplicationNamesUsesAzureFunctionName_IfAzureFunctionMode_IsEnabled(bool functionModeEnabled, string expectedFunctionName, string expectedApplicationNameSource)
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            _localConfig.appSettings.Add(new configurationAdd { key = "AzureFunctionModeEnabled", value = functionModeEnabled.ToString() });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Mock.Arrange(() => _bootstrapConfiguration.AzureFunctionModeDetected).Returns(functionModeEnabled);

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")).Returns("MyAzureFunc");

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "My Application" };

            NrAssert.Multiple(
                () => Assert.That(defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo(expectedFunctionName)),
                () => Assert.That(defaultConfig.ApplicationNamesSource, Is.EqualTo(expectedApplicationNameSource))
            );
        }

        [Test]
        public void ApplicationNameDoesNotUserAzureFunctionName_IfAzureModeIsEnabled_ButAzureFunctionName_IsNullOrEmpty()
        {
            _runTimeConfig.ApplicationNames = new List<string>();

            _localConfig.appSettings.Add(new configurationAdd { key = "AzureFunctionModeEnabled", value = "true" });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Mock.Arrange(() => _bootstrapConfiguration.AzureFunctionModeDetected).Returns(true);

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName)).Returns<string>(null);

            _localConfig.application.name = new List<string> { "My Application" };

            NrAssert.Multiple(
                () => Assert.That(defaultConfig.ApplicationNames.Count(), Is.EqualTo(1)),
                () => Assert.That(defaultConfig.ApplicationNames.FirstOrDefault(), Is.EqualTo("My Application")),
                () => Assert.That(defaultConfig.ApplicationNamesSource, Is.EqualTo("NewRelic Config"))
            );

        }

        #endregion ApplicationNames


        [Test]
        public void AutostartAgentPullsFromLocalConfig()
        {
            _localConfig.service.autoStart = false;
            Assert.That(_defaultConfig.AutoStartAgent, Is.False);

            _localConfig.service.autoStart = true;
            Assert.That(_defaultConfig.AutoStartAgent, Is.True);
        }

        [Test]
        public void UseResourceBasedNamingIsEnabled()
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "NewRelic.UseResourceBasedNamingForWCF",
                value = "true"
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.UseResourceBasedNamingForWCFEnabled, Is.True);
        }

        [Test]
        public void UseResourceBasedNamingIsDisabledByDefault()
        {
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
            Assert.That(defaultConfig.UseResourceBasedNamingForWCFEnabled, Is.False);
        }


        #region CrossApplicationTracingEnabled

        [Test]
        public void CrossApplicationTracingEnabledIsTrueIfAllCatFlagsEnabledAndCrossProcessIdIsNotNull()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
            _serverConfig.CatId = "123#456";

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.True);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsTrueIfCrossApplicationTracerIsMissingButAllOtherFlagsEnabled()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer = null;
            _serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
            _serverConfig.CatId = "123#456";

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.True);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsFalseIfCrossApplicationTracingEnabledIsFalse()
        {
            _localConfig.crossApplicationTracingEnabled = false;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
            _serverConfig.CatId = "123#456";

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.False);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsFalseIfRpmConfigCrossApplicationTracerEnabledIsFalse()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig.RpmConfig.CrossApplicationTracerEnabled = false;
            _serverConfig.CatId = "123#456";

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.False);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsFalseIfCatIdIsNull()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig.RpmConfig.CrossApplicationTracerEnabled = true;
            _serverConfig.CatId = null;

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.False);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsTrueWithNewServerConfig()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig = new ServerConfiguration();
            _serverConfig.CatId = "123#456";
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.True);
        }

        [Test]
        public void CrossApplicationTracingEnabledIsFalseWithGetDefaultServerConfig()
        {
            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig = ServerConfiguration.GetDefault();
            _serverConfig.CatId = "123#456";
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.False);
        }

        [Test]
        public void CrossApplicationTracingEnabledIs_False_InServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);

            _localConfig.crossApplicationTracingEnabled = true;
            _localConfig.crossApplicationTracer.enabled = true;
            _serverConfig = new ServerConfiguration();
            _serverConfig.CatId = "123#456";
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(_defaultConfig.CrossApplicationTracingEnabled, Is.False);
        }

        #endregion CrossApplicationTracingEnabled

        #region Distributed Tracing
        [Test]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void DistributedTracingEnabled(bool localConfig, bool expectedResult)
        {
            _localConfig.distributedTracing.enabled = localConfig;
            Assert.That(_defaultConfig.DistributedTracingEnabled, Is.EqualTo(expectedResult));
        }

        [Test]
        public void DistributedTracingEnabledIsFalseByDefault()
        {
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(_defaultConfig.DistributedTracingEnabled, Is.False);
        }

        [Test]
        [TestCase(null, null)]
        [TestCase("ApplicationIdValue", "ApplicationIdValue")]
        public void PrimaryApplicationIdValue(string server, string expectedResult)
        {
            _serverConfig.PrimaryApplicationId = server;

            Assert.That(expectedResult, Is.EqualTo(_defaultConfig.PrimaryApplicationId));
        }

        [Test]
        [TestCase(null, null)]
        [TestCase("TrustedAccountKey", "TrustedAccountKey")]
        public void TrustedAccountKeyValue(string server, string expectedResult)
        {
            _serverConfig.TrustedAccountKey = server;

            Assert.That(expectedResult, Is.EqualTo(_defaultConfig.TrustedAccountKey));
        }


        [Test]
        [TestCase(null, null)]
        [TestCase("AccountId", "AccountId")]
        public void AccountIdValue(string server, string expectedResult)
        {
            _serverConfig.AccountId = server;

            Assert.That(expectedResult, Is.EqualTo(_defaultConfig.AccountId));
        }

        [Test]
        [TestCase(1234, 1234)]
        public void SamplingTargetValue(int server, int expectedResult)
        {
            _serverConfig.SamplingTarget = server;

            Assert.That(expectedResult, Is.EqualTo(_defaultConfig.SamplingTarget));
        }

        [Test]
        public void SamplingTarget_Is10_InServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.SamplingTarget, Is.EqualTo(10));
        }

        [Test]
        [TestCase(1234, 1234)]
        public void SamplingTargetPeriodInSecondsValue(int server, int expectedResult)
        {
            _serverConfig.SamplingTargetPeriodInSeconds = server;

            Assert.That(expectedResult, Is.EqualTo(_defaultConfig.SamplingTargetPeriodInSeconds));
        }

        [Test]
        public void SamplingTargetPeriodInSeconds_Is60_InServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.SamplingTargetPeriodInSeconds, Is.EqualTo(60));
        }

        [Test]
        public void PrimaryApplicationIdValueIsSetFromEnvironmentVariable_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_PRIMARY_APPLICATION_ID")).Returns("PrimaryApplicationIdValue");

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.PrimaryApplicationId, Is.EqualTo("PrimaryApplicationIdValue"));
        }
        [Test]
        public void TrustedAccountKeyValueIsSetFromEnvironmentVariable_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_TRUSTED_ACCOUNT_KEY")).Returns("TrustedAccountKeyValue");

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.TrustedAccountKey, Is.EqualTo("TrustedAccountKeyValue"));
        }
        [Test]
        public void AccountIdValueIsSetFromEnvironmentVariable_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_ACCOUNT_ID")).Returns("AccountIdValue");

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.AccountId, Is.EqualTo("AccountIdValue"));
        }
        [Test]
        public void PrimaryApplicationId_DefaultsToUnknown_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.PrimaryApplicationId, Is.EqualTo("Unknown"));
        }
        [Test]
        public void PrimaryApplicationId_ComesFromLocalConfig_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            _localConfig.distributedTracing.primary_application_id = "PrimaryApplicationIdValue";

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.PrimaryApplicationId, Is.EqualTo("PrimaryApplicationIdValue"));
        }
        [Test]
        public void TrustedAccountKey_ComesFromLocalConfig_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            _localConfig.distributedTracing.trusted_account_key = "TrustedAccountKeyValue";

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.TrustedAccountKey, Is.EqualTo("TrustedAccountKeyValue"));
        }
        [Test]
        public void AccountId_ComesFromLocalConfig_WhenInServerlessMode()
        {
            // Arrange
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(true);
            _localConfig.distributedTracing.account_id = "AccountIdValue";

            // Act
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            // Assert
            Assert.That(defaultConfig.AccountId, Is.EqualTo("AccountIdValue"));
        }

        #endregion Distributed Tracing

        #region Span Events

        [Test]
        public void SpanEventsEnabledIsTrueInLocalConfigByDefault()
        {
            Assert.That(_localConfig.spanEvents.enabled, Is.True);
        }

        [TestCase(true, true, ExpectedResult = true)]
        [TestCase(true, false, ExpectedResult = false)]
        [TestCase(false, true, ExpectedResult = false)]
        [TestCase(false, false, ExpectedResult = false)]
        public bool SpanEventsEnabledHasCorrectValue(bool distributedTracingEnabled, bool spanEventsEnabled)
        {
            _localConfig.spanEvents.enabled = spanEventsEnabled;
            _localConfig.distributedTracing.enabled = distributedTracingEnabled;

            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            return _defaultConfig.SpanEventsEnabled;
        }

        [Test]
        public void SpanEventsMaxSamplesStoredOverriddenBySpanEventHarvestConfig()
        {
            _localConfig.spanEvents.maximumSamplesStored = 100;

            Assert.That(_defaultConfig.SpanEventsMaxSamplesStored, Is.EqualTo(100));

            _serverConfig.SpanEventHarvestConfig = new SingleEventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimit = 10
            };

            Assert.That(_defaultConfig.SpanEventsMaxSamplesStored, Is.EqualTo(10));
        }

        [Test]
        public void SpanEventsHarvestCycleUsesDefaultOrSpanEventHarvestConfig()
        {
            Assert.That(_defaultConfig.SpanEventsHarvestCycle, Is.EqualTo(TimeSpan.FromMinutes(1)));

            _serverConfig.SpanEventHarvestConfig = new SingleEventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimit = 10
            };

            Assert.That(_defaultConfig.SpanEventsHarvestCycle, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void SpanEventsAttributesEnabled(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.spanEvents.attributes.enabled = localAttributes;
            Assert.That(_defaultConfig.SpanEventsAttributesEnabled, Is.EqualTo(expectedResult));
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
            Assert.That(_defaultConfig.SpanEventsAttributesInclude.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void SpanEventsAttributesIncludeClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.spanEvents.attributes.include = new List<string>(attributes);
            Assert.That(_defaultConfig.SpanEventsAttributesInclude.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void SpanEventsAttributesExclude(string[] attributes, string[] expectedResult)
        {
            _localConfig.spanEvents.attributes.exclude = new List<string>(attributes);
            Assert.That(_defaultConfig.SpanEventsAttributesExclude.Count(), Is.EqualTo(expectedResult.Length));
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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_QUEUE_SIZE")).Returns(envConfigValue);

            if (localConfigValue.HasValue)
            {
                _localConfig.infiniteTracing.span_events.queue_size = localConfigValue.Value;
            }

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
                () => Assert.That(defaultConfig.InfiniteTracingTraceObserverHost, Is.EqualTo(expectedHost)),
                () => Assert.That(defaultConfig.InfiniteTracingTraceObserverPort, Is.EqualTo(expectedPort)),
                () => Assert.That(defaultConfig.InfiniteTracingTraceObserverSsl, Is.EqualTo(expectedSsl))
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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_TIMEOUT_SEND")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_TIMEOUT_CONNECT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_BATCH_SIZE")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_PARTITION_COUNT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_SPAN_EVENTS_STREAMS_COUNT")).Returns(envConfigVal);

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.InfiniteTracingTraceCountConsumers, Is.EqualTo(expectedResult));
        }

        [TestCase("true", "false", ExpectedResult = true)]
        [TestCase("false", "true", ExpectedResult = false)]
        [TestCase(null, "false", ExpectedResult = false)]
        [TestCase("", "false", ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)]
        public bool InfiniteTracing_Compression(string envConfigVal, bool? localConfigVal)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_INFINITE_TRACING_COMPRESSION")).Returns(envConfigVal);

            if (localConfigVal.HasValue)
            {
                _localConfig.infiniteTracing.compression = localConfigVal.Value;
            }

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            return defaultConfig.InfiniteTracingCompression;
        }



        #endregion

        #region Utilization

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectKubernetesConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_KUBERNETES")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectKubernetes = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectKubernetes;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectAwsConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_AWS")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectAws = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectAws;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectAzureConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_AZURE")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectAzure = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectAzure;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectPcfConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_PCF")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectPcf = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectPcf;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectGcpConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_GCP")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectGcp = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectGcp;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectDockerConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_DOCKER")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectDocker = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectDocker;
        }


        [TestCase("true", true, true, ExpectedResult = true)]
        [TestCase("true", false, true, ExpectedResult = true)]
        [TestCase("true", null, true, ExpectedResult = true)]
        [TestCase("false", true, true, ExpectedResult = false)]
        [TestCase("false", false, true, ExpectedResult = false)]
        [TestCase("false", null, true, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, true, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, true, ExpectedResult = true)]
        [TestCase(null, true, true, ExpectedResult = true)]
        [TestCase(null, false, true, ExpectedResult = false)]
        [TestCase(null, null, true, ExpectedResult = true)] // true by default test

        [TestCase("true", true, false, ExpectedResult = false)]
        [TestCase("true", false, false, ExpectedResult = false)]
        [TestCase("true", null, false, ExpectedResult = false)]
        [TestCase("false", true, false, ExpectedResult = false)]
        [TestCase("false", false, false, ExpectedResult = false)]
        [TestCase("false", null, false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", false, false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, false, ExpectedResult = false)]
        [TestCase(null, true, false, ExpectedResult = false)]
        [TestCase(null, false, false, ExpectedResult = false)]
        [TestCase(null, null, false, ExpectedResult = false)] // true by default test
        public bool UtilizationDetectAzureFunctionConfigurationWorksProperly(string environmentSetting, bool? localSetting, bool azureFunctionModeEnabled)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_AZURE_FUNCTION_MODE_ENABLED")).Returns(azureFunctionModeEnabled.ToString());
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_AZURE_FUNCTION")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectAzureFunction = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectAzureFunction;
        }

        [Test]
        public void AzureFunctionModeEnabledByDefault()
        {
            Assert.That(_defaultConfig.AzureFunctionModeEnabled, Is.True, "AzureFunctionMode should be enabled by default");
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = true)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = true)] // true by default test
        public bool UtilizationDetectAzureAppServiceConfigurationWorksProperly(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_UTILIZATION_DETECT_AZURE_APPSERVICE")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.utilization.detectAzureAppService = localSetting.Value;
            }

            return _defaultConfig.UtilizationDetectAzureAppService;
        }

        #endregion

        #region Log Metrics and Events

        [Test]
        public void ApplicationLogging_MetricsEnabled_IsTrueInLocalConfigByDefault()
        {
            Assert.That(_defaultConfig.LogMetricsCollectorEnabled, Is.True);
        }

        [Test]
        public void ApplicationLogging_Enabled_IsTrueInLocalConfigByDefault()
        {
            Assert.That(_defaultConfig.ApplicationLoggingEnabled, Is.True);
        }

        [TestCase(false, false, false, false, false, false, false)]
        [TestCase(false, true, false, false, false, false, false)]
        [TestCase(false, false, true, false, false, false, false)]
        [TestCase(false, false, false, true, false, false, false)]
        [TestCase(false, true, true, true, false, false, false)]
        [TestCase(true, false, false, false, false, false, false)]
        [TestCase(true, true, false, false, true, false, false)]
        [TestCase(true, false, true, false, false, true, false)]
        [TestCase(true, false, false, true, false, false, true)]
        [TestCase(true, true, true, true, true, true, true)]
        public void ApplicationLogging_Enabled_OverridesIndividualLoggingFeatures(bool applicationLoggingEnabledInConfig,
            bool forwardingEnabledInConfig, bool metricsEnabledInConfig, bool localDecoratingEnabledInConfig,
            bool forwardingActuallyEnabled, bool metricsActuallyEnabled, bool localDecoratingActuallyEnabled)
        {
            _localConfig.applicationLogging.enabled = applicationLoggingEnabledInConfig;
            _localConfig.applicationLogging.forwarding.enabled = forwardingEnabledInConfig;
            _localConfig.applicationLogging.metrics.enabled = metricsEnabledInConfig;
            _localConfig.applicationLogging.localDecorating.enabled = localDecoratingEnabledInConfig;

            Assert.Multiple(() =>
            {
                Assert.That(applicationLoggingEnabledInConfig, Is.EqualTo(_defaultConfig.ApplicationLoggingEnabled));
                Assert.That(forwardingActuallyEnabled, Is.EqualTo(_defaultConfig.LogEventCollectorEnabled));
                Assert.That(metricsActuallyEnabled, Is.EqualTo(_defaultConfig.LogMetricsCollectorEnabled));
                Assert.That(localDecoratingActuallyEnabled, Is.EqualTo(_defaultConfig.LogDecoratorEnabled));
            });
        }

        [Test]
        public void ApplicationLogging_ForwardingEnabled_IsTrueInLocalConfigByDefault()
        {
            Assert.That(_defaultConfig.LogEventCollectorEnabled, Is.True);
        }

        [Test]
        public void ApplicationLogging_ForwardingEnabled_IsOverriddenByHighSecurityMode()
        {
            _localConfig.applicationLogging.forwarding.enabled = true;
            _localConfig.highSecurity.enabled = true;

            Assert.That(_defaultConfig.LogEventCollectorEnabled, Is.False);
        }

        [Test]
        public void ApplicationLogging_ForwardingEnabled_IsOverriddenWhenNoSpanEventsAllowed_ByLocalConfig()
        {
            _localConfig.applicationLogging.forwarding.maxSamplesStored = 0;

            Assert.That(_defaultConfig.LogEventCollectorEnabled, Is.False);
        }

        [Test]
        public void ApplicationLogging_ForwardingEnabled_IsOverriddenWhenNoSpanEventsAllowed_ByServer()
        {
            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.LogEventHarvestLimitKey, 0 } }
            };

            Assert.That(_defaultConfig.LogEventCollectorEnabled, Is.False);
        }

        [Test]
        public void ApplicationLogging_LocalDecoratingEnabled_IsFalseInLocalConfigByDefault()
        {
            Assert.That(_defaultConfig.LogDecoratorEnabled, Is.False);
        }

        [Test]
        public void ApplicationLogging_ForwardingMaxSamplesStored_HasCorrectValue()
        {
            _localConfig.applicationLogging.forwarding.maxSamplesStored = 1;
            Assert.That(_defaultConfig.LogEventsMaxSamplesStored, Is.EqualTo(1));
        }

        [Test]
        public void ApplicationLogging_ForwardingLogLevelDeniedList_HasCorrectValue()
        {
            _localConfig.applicationLogging.forwarding.logLevelDenyList = " SomeValue, SomeOtherValue  ";

            Assert.That(_defaultConfig.LogLevelDenyList, Has.Count.EqualTo(2));
            Assert.That(_defaultConfig.LogLevelDenyList, Does.Contain("SOMEVALUE"));
            Assert.That(_defaultConfig.LogLevelDenyList, Does.Contain("SOMEOTHERVALUE"));
        }

        [Test]
        public void LogEventsHarvestCycleUsesDefaultOrEventHarvestConfig()
        {
            const string LogEventHarvestLimitKey = "log_event_data";

            // Confirm default is 5.
            Assert.That(_defaultConfig.LogEventsHarvestCycle.Seconds, Is.EqualTo(5));

            _serverConfig.EventHarvestConfig = new EventHarvestConfig();
            _serverConfig.EventHarvestConfig.ReportPeriodMs = 10000;
            _serverConfig.EventHarvestConfig.HarvestLimits = new Dictionary<string, int>();
            _serverConfig.EventHarvestConfig.HarvestLimits.Add(LogEventHarvestLimitKey, 100); // limit does not matter here

            // Confirm value is set to provided value not default
            Assert.That(_defaultConfig.LogEventsHarvestCycle.Seconds, Is.EqualTo(10));
        }

        [Test]
        public void ApplicationLogging_ContextDataEnabled_IsFalseInLocalConfigByDefault()
        {
            Assert.That(_defaultConfig.ContextDataEnabled, Is.False);
        }

        [TestCase(null, null, ExpectedResult = new string[] { })]
        [TestCase("aaa,bbb", "ccc,ddd", ExpectedResult = new[] { "ccc", "ddd" })]
        [TestCase("aaa,bbb", null, ExpectedResult = new[] { "aaa", "bbb" })]
        [TestCase(null, "ccc,ddd", ExpectedResult = new[] { "ccc", "ddd" })]
        public IEnumerable<string> ApplicationLogging_ContextDataInclude_IsOverriddenByEnvironmentVariable(string local, string environment)
        {
            if (local != null)
            {
                _localConfig.applicationLogging.forwarding.contextData.include = local;
            }

            if (environment != null)
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_INCLUDE")).Returns(environment);
            }

            return _defaultConfig.ContextDataInclude;
        }

        [TestCase(null, null, ExpectedResult = new string[] { "SpanId", "TraceId", "ParentId" })]
        [TestCase("aaa,bbb", "ccc,ddd", ExpectedResult = new[] { "ccc", "ddd" })]
        [TestCase("aaa,bbb", null, ExpectedResult = new[] { "aaa", "bbb" })]
        [TestCase(null, "ccc,ddd", ExpectedResult = new[] { "ccc", "ddd" })]

        public IEnumerable<string> ApplicationLogging_ContextDataExclude_IsOverriddenByEnvironmentVariable(string local, string environment)
        {
            if (local != null)
            {
                _localConfig.applicationLogging.forwarding.contextData.exclude = local;
            }

            if (environment != null)
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_APPLICATION_LOGGING_FORWARDING_CONTEXT_DATA_EXCLUDE")).Returns(environment);
            }

            return _defaultConfig.ContextDataExclude;
        }
        #endregion


        #region Capture Attributes

        [TestCase(null, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void AllowAllHeadersConfigTests(bool? enabled, bool expectedResult)
        {
            if (enabled.HasValue)
            {
                _localConfig.allowAllHeaders.enabled = enabled.Value;
            }

            Assert.That(_defaultConfig.AllowAllRequestHeaders, Is.EqualTo(expectedResult));
        }

        [TestCase(true, false)]
        [TestCase(false, false)]
        public void AllowAllHeaders_HighSecurityMode_Enabled_Tests(bool enabled, bool expectedResult)
        {
            _localConfig.allowAllHeaders.enabled = enabled;
            _localConfig.highSecurity.enabled = true;

            Assert.Multiple(() =>
            {
                Assert.That(_defaultConfig.AllowAllRequestHeaders, Is.EqualTo(expectedResult));
                Assert.That(_defaultConfig.CaptureAttributesIncludes.Count(), Is.EqualTo(0));
            });
        }

        [TestCase(true, true)]
        [TestCase(false, false)]
        public void CaptureAttributes(bool captureAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = captureAttributes;
            Assert.That(_defaultConfig.CaptureAttributes, Is.EqualTo(expectedResult));
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

            Assert.That(_defaultConfig.CaptureAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureAttributuesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.attributes.include = new List<string>(attributes);

            Assert.That(_defaultConfig.CaptureAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void CaptureTransactionEventsAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.transactionEvents.attributes.enabled = localAttributes;
            Assert.That(_defaultConfig.TransactionEventsAttributesEnabled, Is.EqualTo(expectedResult));
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

            Assert.That(_defaultConfig.TransactionEventsAttributesInclude.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureTransactionEventAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.transactionEvents.attributes.include = new List<string>(attributes);

            Assert.That(_defaultConfig.TransactionEventsAttributesInclude.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void CaptureTransactionTraceAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.transactionTracer.attributes.enabled = localAttributes;
            Assert.That(_defaultConfig.CaptureTransactionTraceAttributes, Is.EqualTo(expectedResult));
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

            Assert.That(_defaultConfig.CaptureTransactionTraceAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureTransactionTraceAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.transactionTracer.attributes.include = new List<string>(attributes);

            Assert.That(_defaultConfig.CaptureTransactionTraceAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void CaptureErrorCollectorAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.errorCollector.attributes.enabled = localAttributes;
            Assert.That(_defaultConfig.CaptureErrorCollectorAttributes, Is.EqualTo(expectedResult));
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

            Assert.That(_defaultConfig.CaptureErrorCollectorAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureErrorCollectorAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.errorCollector.attributes.include = new List<string>(attributes);

            Assert.That(_defaultConfig.CaptureErrorCollectorAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, true)]
        [TestCase(true, false, false)]
        public void CaptureBrowserMonitoringAttributes(bool globalAttributes, bool localAttributes, bool expectedResult)
        {
            _localConfig.attributes.enabled = globalAttributes;
            _localConfig.browserMonitoring.attributes.enabled = localAttributes;
            Assert.That(_defaultConfig.CaptureBrowserMonitoringAttributes, Is.EqualTo(expectedResult));
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

            Assert.That(_defaultConfig.CaptureBrowserMonitoringAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
        }

        [TestCase(false, new[] { "att1", "att2" }, new string[] { })]
        [TestCase(true, new[] { "att1", "att2" }, new[] { "att1", "att2" })]
        public void CaptureBrowserMonitoringAttributesIncludesClearedBySecurityPolicy(bool securityPolicyEnabled, string[] attributes, string[] expectedResult)
        {
            SetupNewConfigsWithSecurityPolicy("attributes_include", securityPolicyEnabled);
            _localConfig.browserMonitoring.attributes.enabled = true;
            _localConfig.browserMonitoring.attributes.include = new List<string>(attributes);

            Assert.That(_defaultConfig.CaptureBrowserMonitoringAttributesIncludes.Count(), Is.EqualTo(expectedResult.Length));
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
            Assert.That(_defaultConfig.CustomEventsMaximumSamplesStored, Is.EqualTo(30000));

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

            Assert.That(_defaultConfig.CustomEventsMaximumSamplesStored, Is.EqualTo(10));
        }

        [TestCase("10", 20, 30, ExpectedResult = 30)]
        [TestCase("10", null, 30, ExpectedResult = 30)]
        [TestCase("10", 20, null, ExpectedResult = 10)]
        [TestCase("10", null, null, ExpectedResult = 10)]
        [TestCase(null, 20, 30, ExpectedResult = 30)]
        [TestCase(null, null, 30, ExpectedResult = 30)]
        [TestCase(null, 20, null, ExpectedResult = 20)]
        [TestCase(null, null, null, ExpectedResult = 30000)]
        public int CustomEventsMaxSamplesStoredOverriddenByEnvironment(string environmentSetting, int? localSetting, int? serverSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("MAX_EVENT_SAMPLES_STORED")).Returns(environmentSetting);

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
            Assert.That(_defaultConfig.CustomEventsHarvestCycle, Is.EqualTo(TimeSpan.FromMinutes(1)));

            _serverConfig.EventHarvestConfig = new EventHarvestConfig
            {
                ReportPeriodMs = 5000,
                HarvestLimits = new Dictionary<string, int> { { EventHarvestConfig.CustomEventHarvestLimitKey, 10 } }
            };
            Assert.That(_defaultConfig.CustomEventsHarvestCycle, Is.EqualTo(TimeSpan.FromSeconds(5)));
        }

        [Test]
        public void CustomEventsMaxSamplesOf0ShouldDisableCustomEvents()
        {
            _localConfig.customEvents.maximumSamplesStored = 0;
            Assert.That(_defaultConfig.CustomEventsEnabled, Is.False);
        }

        #endregion

        #region SecurityPolicies

        [TestCase(null, null, ExpectedResult = "")]
        [TestCase(null, "localValue", ExpectedResult = "localValue")]
        [TestCase("envValue", null, ExpectedResult = "envValue")]
        [TestCase("envValue", "localValue", ExpectedResult = "envValue")]
        [TestCase("", "localValue", ExpectedResult = "localValue")]
        [TestCase("  ", "localValue", ExpectedResult = "localValue")]
        [TestCase("  envValue  ", null, ExpectedResult = "envValue")]
        [TestCase(null, "  localValue  ", ExpectedResult = "localValue")]
        public string SecurityPoliciesTokenReturned(string environmentValue, string localConfigValue)
        {
            _localConfig.securityPoliciesToken = localConfigValue;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_SECURITY_POLICIES_TOKEN"))
                .Returns(environmentValue);
            return _defaultConfig.SecurityPoliciesToken;
        }

        [TestCase(null, null, ExpectedResult = false)]
        [TestCase(null, "localValue", ExpectedResult = true)]
        [TestCase("envValue", null, ExpectedResult = true)]
        [TestCase("envValue", "localValue", ExpectedResult = true)]
        [TestCase("", "localValue", ExpectedResult = true)]
        [TestCase("envValue", "", ExpectedResult = true)]
        [TestCase("", "", ExpectedResult = false)]
        public bool SecurityPoliciesTokenExists(string environmentValue, string localConfigValue)
        {
            _localConfig.securityPoliciesToken = localConfigValue;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_SECURITY_POLICIES_TOKEN"))
                .Returns(environmentValue);
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

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
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            return defaultConfig.ForceSynchronousTimingCalculationHttpClient;
        }

        [TestCase(null, ExpectedResult = true)]
        [TestCase("not a bool", ExpectedResult = true)]
        [TestCase("false", ExpectedResult = false)]
        [TestCase("true", ExpectedResult = true)]
        public bool AspNetCore6PlusBrowserInjectionTests(string localConfigValue)
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "EnableAspNetCore6PlusBrowserInjection", value = localConfigValue });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            return defaultConfig.EnableAspNetCore6PlusBrowserInjection;
        }

        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("true", false, ExpectedResult = true)]
        [TestCase("true", null, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("false", false, ExpectedResult = false)]
        [TestCase("false", null, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", true, ExpectedResult = true)]
        [TestCase("invalidEnvVarValue", false, ExpectedResult = false)]
        [TestCase("invalidEnvVarValue", null, ExpectedResult = false)]
        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase(null, null, ExpectedResult = false)] // false by default test
        public bool GloballyForceNewTransactionConfigurationTests(string environmentSetting, bool? localSetting)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_FORCE_NEW_TRANSACTION_ON_NEW_THREAD")).Returns(environmentSetting);

            if (localSetting.HasValue)
            {
                _localConfig.service.forceNewTransactionOnNewThread = localSetting.Value;
            }

            return _defaultConfig.ForceNewTransactionOnNewThread;
        }

        [Test]
        public void CodeLevelMetricsAreEnabledByDefault()
        {
            Assert.That(_defaultConfig.CodeLevelMetricsEnabled, Is.True, "Code Level Metrics should be enabled by default");
        }

        [TestCase(true, null, ExpectedResult = true)]
        [TestCase(true, "true", ExpectedResult = true)]
        [TestCase(true, "1", ExpectedResult = true)]
        [TestCase(true, "false", ExpectedResult = false)]
        [TestCase(true, "0", ExpectedResult = false)]
        [TestCase(true, "invalid", ExpectedResult = true)]
        [TestCase(false, null, ExpectedResult = false)]
        [TestCase(false, "true", ExpectedResult = true)]
        [TestCase(false, "1", ExpectedResult = true)]
        [TestCase(false, "false", ExpectedResult = false)]
        [TestCase(false, "0", ExpectedResult = false)]
        [TestCase(false, "invalid", ExpectedResult = false)]
        [TestCase(null, "true", ExpectedResult = true)]
        [TestCase(null, "1", ExpectedResult = true)]
        [TestCase(null, "false", ExpectedResult = false)]
        [TestCase(null, "0", ExpectedResult = false)]
        [TestCase(null, "invalid", ExpectedResult = true)]
        [TestCase(null, null, ExpectedResult = true)]
        public bool ShouldCodeLevelMetricsBeEnabled(bool? localConfigValue, string envConfigValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_CODE_LEVEL_METRICS_ENABLED")).Returns(envConfigValue);

            if (localConfigValue.HasValue)
            {
                _localConfig.codeLevelMetrics.enabled = localConfigValue.Value;
            }

            return _defaultConfig.CodeLevelMetricsEnabled;
        }

        #region Harvest Cycle Tests

        [Test]
        public void HarvestCycleOverride_DefaultOrNotSet()
        {
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.Multiple(() =>
            {
                Assert.That(defaultConfig.MetricsHarvestCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.TransactionTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.ErrorTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.GetAgentCommandsCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.SpanEventsHarvestCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.SqlTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
                Assert.That(defaultConfig.StackExchangeRedisCleanupCycle.TotalSeconds, Is.EqualTo(60));
            });
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_Metrics_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideMetricsHarvestCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.MetricsHarvestCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_TransactionTraces_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideTransactionTracesHarvestCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.TransactionTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_ErrorTraces_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideErrorTracesHarvestCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.ErrorTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_SpanEvents_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSpanEventsHarvestCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.SpanEventsHarvestCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_GetAgentCommands_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideGetAgentCommandsCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.GetAgentCommandsCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_SqlTraces_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSqlTracesHarvestCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.SqlTracesHarvestCycle.TotalSeconds, Is.EqualTo(60));
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("-1")]
        [TestCase("")]
        [TestCase("a")]
        public void HarvestCycleOverride_StackExchangeRedisCleanup_NotValidValueSet(string value)
        {
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideStackExchangeRedisCleanupCycle",
                value = value
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.StackExchangeRedisCleanupCycle.TotalSeconds, Is.EqualTo(60));
        }

        [Test]
        public void HarvestCycleOverride_Metrics_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideMetricsHarvestCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.MetricsHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideMetricsHarvestCycle",
                value = "100"
            });

            Assert.That(defaultConfig.MetricsHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_TransactionTraces_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideTransactionTracesHarvestCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.TransactionTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideTransactionTracesHarvestCycle",
                value = "100"
            });

            Assert.That(defaultConfig.TransactionTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_ErrorTraces_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideErrorTracesHarvestCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.ErrorTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideErrorTracesHarvestCycle",
                value = "100"
            });

            Assert.That(defaultConfig.ErrorTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_SpanEvents_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSpanEventsHarvestCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.SpanEventsHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSpanEventsHarvestCycle",
                value = "100"
            });

            Assert.That(defaultConfig.SpanEventsHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_GetAgentCommands_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideGetAgentCommandsCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.GetAgentCommandsCycle.Seconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideGetAgentCommandsCycle",
                value = "100"
            });

            Assert.That(defaultConfig.GetAgentCommandsCycle.Seconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_SqlTraces_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSqlTracesHarvestCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.SqlTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideSqlTracesHarvestCycle",
                value = "100"
            });

            Assert.That(defaultConfig.SqlTracesHarvestCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        [Test]
        public void HarvestCycleOverride_StackExchangeRedisCleanup_ValidValueSet()
        {
            var expectedSeconds = "10";
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideStackExchangeRedisCleanupCycle",
                value = expectedSeconds
            });

            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            Assert.That(defaultConfig.StackExchangeRedisCleanupCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));

            // Test that the backing field is used after the initial call and not changed.
            _localConfig.appSettings.Add(new configurationAdd()
            {
                key = "OverrideStackExchangeRedisCleanupCycle",
                value = "100"
            });

            Assert.That(defaultConfig.StackExchangeRedisCleanupCycle.TotalSeconds, Is.EqualTo(Convert.ToInt32(expectedSeconds)));
        }

        #endregion

        #region Ignored Instrumentation Tests

        [Test]
        public void NoIgnoredInstrumentationByDefault()
        {
            Assert.That(_defaultConfig.IgnoredInstrumentation, Is.Empty);
        }

        [Test]
        public void IgnoredInstrumentationDoesNotRequireClassName()
        {
            var expectedList = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "assemblyName", "Assembly1" },
                    { "className", null }
                }
            };

            _localConfig.instrumentation.rules.Add(new configurationInstrumentationIgnore { assemblyName = "Assembly1" });

            Assert.That(_defaultConfig.IgnoredInstrumentation, Is.EquivalentTo(expectedList));
        }

        [Test]
        public void IgnoredInstrumentationCanIncludeClassName()
        {
            var expectedList = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "assemblyName", "Assembly1" },
                    { "className", "Class1" }
                }
            };

            _localConfig.instrumentation.rules.Add(new configurationInstrumentationIgnore { assemblyName = "Assembly1", className = "Class1" });

            Assert.That(_defaultConfig.IgnoredInstrumentation, Is.EquivalentTo(expectedList));
        }

        [Test]
        public void IgnoredInstrumentationCanHaveMultipleItems()
        {
            var expectedList = new List<IDictionary<string, string>>
            {
                new Dictionary<string, string>
                {
                    { "assemblyName", "Assembly1" },
                    { "className", null }
                },
                new Dictionary<string, string>
                {
                    { "assemblyName", "Assembly2" },
                    { "className", "Class2" }
                }
            };

            _localConfig.instrumentation.rules.Add(new configurationInstrumentationIgnore { assemblyName = "Assembly1" });
            _localConfig.instrumentation.rules.Add(new configurationInstrumentationIgnore { assemblyName = "Assembly2", className = "Class2" });

            Assert.That(_defaultConfig.IgnoredInstrumentation, Is.EquivalentTo(expectedList));
        }

        #endregion

        #region AI Monitoring Tests
        [Test]
        public void AiMonitoringDisabledByDefault()
        {
            Assert.That(_defaultConfig.AiMonitoringEnabled, Is.False);
        }
        [Test]
        public void AiMonitoringEnabledByLocalConfig()
        {
            _localConfig.aiMonitoring.enabled = true;
            Assert.That(_defaultConfig.AiMonitoringEnabled, Is.True);
        }
        [Test]
        public void AiMonitoringEnabledByEnvironmentVariable()
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_AI_MONITORING_ENABLED")).Returns("true");
            Assert.That(_defaultConfig.AiMonitoringEnabled, Is.True);
        }
        [Test]
        public void AiMonitoringDisabledWhenHighSecurityModeEnabled()
        {
            _localConfig.highSecurity.enabled = true;
            _localConfig.aiMonitoring.enabled = true;
            Assert.That(_defaultConfig.AiMonitoringEnabled, Is.False);
        }

        [Test]
        public void AiMonitoringStreamingDisabledByLocalConfig()
        {
            _localConfig.aiMonitoring.enabled = true;
            _localConfig.aiMonitoring.streaming.enabled = false;
            Assert.That(_defaultConfig.AiMonitoringStreamingEnabled, Is.False);
        }
        [Test]
        public void AiMonitoringStreamingEnabledByDefaultWhenAiMonitoringEnabled()
        {
            _localConfig.aiMonitoring.enabled = true;
            Assert.That(_defaultConfig.AiMonitoringStreamingEnabled, Is.True);
        }
        [Test]
        public void AiMonitoringStreamingDisabledByEnvironmentVariableWhenAiMonitoringEnabled()
        {
            _localConfig.aiMonitoring.enabled = true;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_AI_MONITORING_STREAMING_ENABLED")).Returns("false");
            Assert.That(_defaultConfig.AiMonitoringStreamingEnabled, Is.False);
        }

        [Test]
        public void AiMonitoringRecordContentEnabledWhenAiMonitoringEnabled()
        {
            _localConfig.aiMonitoring.enabled = true;
            Assert.That(_defaultConfig.AiMonitoringRecordContentEnabled, Is.True);
        }
        [Test]
        public void AiMonitoringRecordContentDisabledByLocalConfig()
        {
            _localConfig.aiMonitoring.enabled = true;
            _localConfig.aiMonitoring.recordContent.enabled = false;
            Assert.That(_defaultConfig.AiMonitoringRecordContentEnabled, Is.False);
        }
        [Test]
        public void AiMonitoringRecordContentDisabledByEnvironmentVariable()
        {
            _localConfig.aiMonitoring.enabled = true;
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_AI_MONITORING_RECORD_CONTENT_ENABLED")).Returns("false");
            Assert.That(_defaultConfig.AiMonitoringRecordContentEnabled, Is.False);
        }
        [Test]
        public void AiMonitoringRecordContentDisabledWhenAiMonitoringDisabled()
        {
            _localConfig.aiMonitoring.enabled = false;
            Assert.That(_defaultConfig.AiMonitoringRecordContentEnabled, Is.False);
        }

        [Test]
        public void LlmTokenCountingCallbackComesFromRuntimeConfig()
        {
            var runtimeConfig = new RunTimeConfiguration(Enumerable.Empty<string>(), null, (s1, s2) => 42);
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, runtimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
            Assert.That(defaultConfig.LlmTokenCountingCallback("foo", "bar"), Is.EqualTo(42));
        }
        #endregion

        #region Agent Logs

        [TestCase(null, true, ExpectedResult = true)]
        [TestCase(null, false, ExpectedResult = false)]
        [TestCase("true", true, ExpectedResult = true)]
        [TestCase("false", true, ExpectedResult = false)]
        [TestCase("1", true, ExpectedResult = true)]
        [TestCase("0", true, ExpectedResult = false)]
        [TestCase("True", true, ExpectedResult = true)]
        [TestCase("False", true, ExpectedResult = false)]
        public bool LoggingEnabledTests(string environmentValue, bool localConfigValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_LOG_ENABLED")).Returns(environmentValue);
            _localConfig.log.enabled = localConfigValue;

            return _defaultConfig.LoggingEnabled;
        }

        [Test]
        public void LoggingEnabledValueIsCached()
        {
            _localConfig.log.enabled = true;

            var firstLoggingEnabledValue = _defaultConfig.LoggingEnabled;

            _localConfig.log.enabled = false;

            var secondLoggingEnabledValue = _defaultConfig.LoggingEnabled;

            Assert.Multiple(() =>
            {
                Assert.That(firstLoggingEnabledValue, Is.True);
                Assert.That(secondLoggingEnabledValue, Is.True);
            });
        }

        [TestCase(null, "finest", ExpectedResult = "FINEST")]
        [TestCase(null, "debug", ExpectedResult = "DEBUG")]
        [TestCase("debug", "finest", ExpectedResult = "DEBUG")]
        [TestCase("info", "finest", ExpectedResult = "INFO")]
        public string LoggingLevelTests(string environmentValue, string localConfigValue)
        {
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_LOG_LEVEL", "NEWRELIC_LOG_LEVEL")).Returns(environmentValue);
            _localConfig.log.level = localConfigValue;

            return _defaultConfig.LoggingLevel;
        }

        [Test]
        public void LoggingLevelIsOffWhenNotEnabled()
        {
            _localConfig.log.level = "info";
            _localConfig.log.enabled = false;

            Assert.That(_defaultConfig.LoggingLevel, Is.EqualTo("off"));
        }

        [Test]
        public void LoggingLevelValueIsCached()
        {
            _localConfig.log.level = "debug";

            var firstLoggingLevelValue = _defaultConfig.LoggingLevel;

            _localConfig.log.level = "finest";

            var secondLoggingLevelValue = _defaultConfig.LoggingLevel;

            Assert.Multiple(() =>
            {
                Assert.That(firstLoggingLevelValue, Is.EqualTo("DEBUG"));
                Assert.That(secondLoggingLevelValue, Is.EqualTo("DEBUG"));
            });
        }

        #endregion Agent Logs

        [TestCase(false, null, true, false, ExpectedResult = false)] // default
        [TestCase(true, null, true, false, ExpectedResult = true)]
        [TestCase(false, true, true, false, ExpectedResult = true)]
        [TestCase(true, false, true, false, ExpectedResult = false)]
        [TestCase(false, null, false, false, ExpectedResult = true)]
        [TestCase(false, null, false, true, ExpectedResult = true)]
        [TestCase(false, null, true, true, ExpectedResult = true)]
        [TestCase(true, null, false, false, ExpectedResult = true)]
        [TestCase(true, null, false, true, ExpectedResult = true)]
        [TestCase(true, null, true, true, ExpectedResult = true)]
        [TestCase(false, true, false, false, ExpectedResult = true)]
        [TestCase(false, true, false, true, ExpectedResult = true)]
        [TestCase(false, true, true, true, ExpectedResult = true)]
        [TestCase(true, false, false, false, ExpectedResult = true)]
        [TestCase(true, false, false, true, ExpectedResult = true)]
        [TestCase(true, false, true, true, ExpectedResult = true)]
        [TestCase(true, true, true, true, ExpectedResult = true)]
        [TestCase(false, false, false, false, ExpectedResult = true)]
        public bool ValidateDisableFileSystemWatcher(bool localWatcherDisabled, bool? envWatcherDisabled, bool loggingEnabled, bool serverlessMode)
        {
            // Setup config values
            _localConfig.service.disableFileSystemWatcher = localWatcherDisabled;

            if (envWatcherDisabled.HasValue)
            {
                Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISABLE_FILE_SYSTEM_WATCHER")).Returns(envWatcherDisabled.ToString().ToLower());
            }

            _localConfig.log.enabled = loggingEnabled;
            Mock.Arrange(() => _bootstrapConfiguration.ServerlessModeEnabled).Returns(serverlessMode);

            // test
            return _defaultConfig.DisableFileSystemWatcher;
        }

        #region Azure Function config tests
        [Test]
        [TestCase(null, "some-subscription-id+resourcegroup-region-Linux", false)] // resource id can be parsed from WEBSITE_OWNER_NAME
        [TestCase("", "some-subscription-id+resourcegroup-region-Linux", false)] // resource id can be parsed from WEBSITE_OWNER_NAME
        [TestCase(null, "website-owner", false)] // resource id cannot be parsed from WEBSITE_OWNER_NAME
        [TestCase("", "website-owner", false)] // resource id can be parsed from WEBSITE_OWNER_NAME
        [TestCase("resourceGroup", null, true)]
        [TestCase("resourceGroup", "", true)]
        public void AzureFunctionResourceId_ShouldReturnEmpty_WhenResourceGroupOrSubscriptionIdIsNullOrEmpty(string resourceGroup, string websiteOwner, bool expectEmpty)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns(resourceGroup);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns(websiteOwner);

            // Act
            var result = _defaultConfig.AzureFunctionResourceId;

            // Assert
            Assert.That(result, expectEmpty ? Is.Empty : Is.Not.Empty);
        }

        [Test]
        public void AzureFunctionResourceId_ShouldReturnCorrectResourceId_WhenResourceGroupAndSubscriptionIdAreNotEmpty()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns("some-resource-group");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("some-subscription-id+resourcegroup-region-Linux");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")).Returns("some-service-name");

            // Act
            var result = _defaultConfig.AzureFunctionResourceId;

            // Assert
            var expected = "/subscriptions/some-subscription-id/resourceGroups/some-resource-group/providers/Microsoft.Web/sites/some-service-name";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void AzureFunctionResourceId_ShouldReturnUnknownServiceName_WhenAzureFunctionServiceNameIsNull()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns("some-resource-group");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("some-subscription-id+resourcegroup-region-Linux");

            // Act
            var result = _defaultConfig.AzureFunctionResourceId;

            // Assert
            var expected = "/subscriptions/some-subscription-id/resourceGroups/some-resource-group/providers/Microsoft.Web/sites/unknown";
            Assert.That(result, Is.EqualTo(expected));
        }


        [Test]
        public void AzureFunctionResourceIdWithFunctionName_ShouldReturnEmpty_WhenResourceIdIsEmpty()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns((string)null);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns((string)null);

            // Act
            var result = _defaultConfig.AzureFunctionResourceIdWithFunctionName("some-function");

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AzureFunctionResourceIdWithFunctionName_ShouldReturnEmpty_WhenFunctionNameIsEmpty()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns("some-resource-group");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("some-subscription-id+resourcegroup-region-Linux");

            // Act
            var result = _defaultConfig.AzureFunctionResourceIdWithFunctionName(null);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AzureFunctionResourceIdWithFunctionName_ShouldReturnCorrectResourceIdWithFunctionName_WhenResourceIdIsNotEmpty()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns("some-resource-group");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("some-subscription-id+resourcegroup-region-Linux");
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")).Returns("some-service-name");

            // Act
            var result = _defaultConfig.AzureFunctionResourceIdWithFunctionName("some-function");

            // Assert
            var expected = "/subscriptions/some-subscription-id/resourceGroups/some-resource-group/providers/Microsoft.Web/sites/some-service-name/functions/some-function";
            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void AzureFunctionResourceGroupName_ShouldReturnWebsiteResourceGroup_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns("some-resource-group");

            // Act
            var result = _defaultConfig.AzureFunctionResourceGroupName;

            // Assert
            Assert.That(result, Is.EqualTo("some-resource-group"));
        }

        [Test]
        public void AzureFunctionResourceGroupName_ShouldReturnParsedResourceGroup_WhenWebsiteOwnerNameIsSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns(string.Empty);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("subscription+resourcegroup-region-Linux");

            // Act
            var result = _defaultConfig.AzureFunctionResourceGroupName;

            // Assert
            Assert.That(result, Is.EqualTo("resourcegroup"));
        }

        [Test]
        public void AzureFunctionResourceGroupName_ShouldReturnWebsiteOwnerName_WhenFormatIsUnexpected()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns(string.Empty);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("unexpected-format");

            // Act
            var result = _defaultConfig.AzureFunctionResourceGroupName;

            // Assert
            Assert.That(result, Is.EqualTo("unexpected-format"));
        }

        [Test]
        public void AzureFunctionResourceGroupName_ShouldReturnEmptyString_WhenWebsiteOwnerNameIsEmpty()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_RESOURCE_GROUP")).Returns(string.Empty);
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns(string.Empty);

            // Act
            var result = _defaultConfig.AzureFunctionResourceGroupName;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void AzureFunctionRegion_ShouldReturnRegionName_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("REGION_NAME")).Returns("some-region");

            // Act
            var result = _defaultConfig.AzureFunctionRegion;

            // Assert
            Assert.That(result, Is.EqualTo("some-region"));
        }

        [Test]
        public void AzureFunctionSubscriptionId_ShouldReturnSubscriptionId_WhenWebsiteOwnerNameIsSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("subscription+resourcegroup-region-Linux");

            // Act
            var result = _defaultConfig.AzureFunctionSubscriptionId;

            // Assert
            Assert.That(result, Is.EqualTo("subscription"));
        }

        [Test]
        public void AzureFunctionSubscriptionId_ShouldReturnWebsiteOwnerName_WhenFormatIsUnexpected()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns("unexpected-format");

            // Act
            var result = _defaultConfig.AzureFunctionSubscriptionId;

            // Assert
            Assert.That(result, Is.EqualTo("unexpected-format"));
        }

        [Test]
        public void AzureFunctionSubscriptionId_ShouldReturnEmptyString_WhenWebsiteOwnerNameIsNotSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_OWNER_NAME")).Returns(string.Empty);

            // Act
            var result = _defaultConfig.AzureFunctionSubscriptionId;

            // Assert
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void AzureFunctionServiceName_ShouldReturnServiceName_WhenEnvironmentVariableIsSet()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")).Returns("some-service-name");

            // Act
            var result = _defaultConfig.AzureFunctionAppName;

            // Assert
            Assert.That(result, Is.EqualTo("some-service-name"));
        }
        #endregion

        #region Cloud
        [Test]
        public void Cloud_Section_Parsing_And_Override()
        {
            string xmlString = """
            <?xml version="1.0"?>
            <configuration xmlns="urn:newrelic-config" agentEnabled="true">
              <cloud>
                <aws accountId="123456789012" />
              </cloud>
            </configuration>
            """;
            var config = GenerateConfigFromXml(xmlString);

            Assert.That(config.AwsAccountId, Is.EqualTo("123456789012"));

            xmlString = """
            <?xml version="1.0"?>
            <configuration xmlns="urn:newrelic-config" agentEnabled="true">
              <cloud>
                <aws />
              </cloud>
            </configuration>
            """;
            config = GenerateConfigFromXml(xmlString);

            Assert.That(config.AwsAccountId, Is.Null);

            xmlString = """
            <?xml version="1.0"?>
            <configuration xmlns="urn:newrelic-config" agentEnabled="true">
              <cloud>
              </cloud>
            </configuration>
            """;
            config = GenerateConfigFromXml(xmlString);

            Assert.That(config.AwsAccountId, Is.Null);

            xmlString = """
            <?xml version="1.0"?>
            <configuration xmlns="urn:newrelic-config" agentEnabled="true">
            </configuration>
            """;
            config = GenerateConfigFromXml(xmlString);

            Assert.That(config.AwsAccountId, Is.Null);

            // null from the last test, but env override should work
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_CLOUD_AWS_ACCOUNT_ID")).Returns("444488881212");

            Assert.That(config.AwsAccountId, Is.EqualTo("444488881212"));

            // A second call should use the cached value
            Assert.That(config.AwsAccountId, Is.EqualTo("444488881212"));

            // If it exists in the config, the env variable should still override
            xmlString = """
            <?xml version="1.0"?>
            <configuration xmlns="urn:newrelic-config" agentEnabled="true">
              <cloud>
                <aws accountId="123456789012" />
              </cloud>
            </configuration>
            """;
            config = GenerateConfigFromXml(xmlString);
            Assert.That(config.AwsAccountId, Is.EqualTo("444488881212"));
        }

        #endregion

        [Test]
        public void InvalidLicenseKey_SetsLicenseKeyMissing_AgentControlStatus()
        {
            // Arrange
            var healthCheck = new HealthCheck();
            Mock.Arrange(() => _agentHealthReporter.SetAgentControlStatus(Arg.IsAny<(bool IsHealthy, string Code, string Status)>(), Arg.IsAny<string[]>()))
                .DoInstead((ValueTuple<bool, string, string> healthStatus, string[] statusParams) =>
                {
                    healthCheck.TrySetHealth(healthStatus, statusParams);
                });

            CreateDefaultConfiguration();

            // Act
            var licenseKey = _defaultConfig.AgentLicenseKey;

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(licenseKey, Is.EqualTo(string.Empty));
                Assert.That(healthCheck.IsHealthy, Is.False);
                Assert.That(healthCheck.Status, Is.EqualTo("License key missing in configuration"));
                Assert.That(healthCheck.LastError, Is.EqualTo("NR-APM-002"));
            });
        }

        [Test]
        public void MissingApplicationName_SetsApplicationNameMissing_AgentControlStatus()
        {
            var healthCheck = new HealthCheck();
            Mock.Arrange(() => _agentHealthReporter.SetAgentControlStatus(Arg.IsAny<(bool IsHealthy, string Code, string Status)>(), Arg.IsAny<string[]>()))
                .DoInstead((ValueTuple<bool, string, string> healthStatus, string[] statusParams) =>
                {
                    healthCheck.TrySetHealth(healthStatus, statusParams);
                });

            _runTimeConfig.ApplicationNames = new List<string>();

            //Sets to default return null for all calls unless overriden by later arrange.
            Mock.Arrange(() => _environment.GetEnvironmentVariable(Arg.IsAny<string>())).Returns<string>(null);

            Mock.Arrange(() => _configurationManagerStatic.GetAppSetting(Constants.AppSettingsAppName))
                .Returns<string>(null);

            _localConfig.application.name = new List<string>();

            Mock.Arrange(() => _httpRuntimeStatic.AppDomainAppVirtualPath).Returns("NotNull");
            Mock.Arrange(() => _processStatic.GetCurrentProcess().ProcessName).Returns((string)null);

            CreateDefaultConfiguration();

            // Act
            Assert.Throws<Exception>(() => _defaultConfig.ApplicationNames.ToList());

            // Assert
            Assert.Multiple(() =>
            {
                Assert.That(healthCheck.IsHealthy, Is.False);
                Assert.That(healthCheck.Status, Is.EqualTo("Missing application name in agent configuration"));
                Assert.That(healthCheck.LastError, Is.EqualTo("NR-APM-005"));
            });

        }

        [TestCase("alwaysOn", RemoteParentSampledBehavior.AlwaysOn, TestName = "RemoteParentSampledBehavior_AlwaysOn_EnvironmentVariableOverride")]
        [TestCase("AlWaYSOn", RemoteParentSampledBehavior.AlwaysOn, TestName = "RemoteParentSampledBehavior_AlwaysOnMixedCase_EnvironmentVariableOverride")]
        [TestCase("alwaysOff", RemoteParentSampledBehavior.AlwaysOff, TestName = "RemoteParentSampledBehavior_AlwaysOff_EnvironmentVariableOverride")]
        [TestCase("default", RemoteParentSampledBehavior.Default, TestName = "RemoteParentSampledBehavior_Default_EnvironmentVariableOverride")]
        [TestCase("invalidValue", RemoteParentSampledBehavior.Default, TestName = "RemoteParentSampledBehavior_InvalidValueDefaultsToDefault_EnvironmentVariableOverride")]
        public void RemoteParentSampledBehavior_UsesEnvironmentVariableOverride(string environmentVariableValue, RemoteParentSampledBehavior expectedRemoteParentSampledBehavior)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns(environmentVariableValue);

            // Act
            var result = _defaultConfig.RemoteParentSampledBehavior;

            // Assert
            Assert.That(result, Is.EqualTo(expectedRemoteParentSampledBehavior));
        }

        [TestCase("alwaysOn", RemoteParentSampledBehavior.AlwaysOn, TestName = "RemoteParentNotSampledBehavior_AlwaysOn_EnvironmentVariableOverride")]
        [TestCase("alwaysOff", RemoteParentSampledBehavior.AlwaysOff, TestName = "RemoteParentNotSampledBehavior_AlwaysOff_EnvironmentVariableOverride")]
        [TestCase("default", RemoteParentSampledBehavior.Default, TestName = "RemoteParentNotSampledBehavior_Default_EnvironmentVariableOverride")]
        [TestCase("invalidValue", RemoteParentSampledBehavior.Default, TestName = "RemoteParentNotSampledBehavior_InvalidValueDefaultsToDefault_EnvironmentVariableOverride")]
        public void RemoteParentNotSampledBehavior_UsesEnvironmentVariableOverride(string environmentVariableValue, RemoteParentSampledBehavior expectedRemoteParentSampledBehavior)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED"))
                .Returns(environmentVariableValue);

            // Act
            var result = _defaultConfig.RemoteParentNotSampledBehavior;

            // Assert
            Assert.That(result, Is.EqualTo(expectedRemoteParentSampledBehavior));
        }
        [Test]
        public void IncludedActivitySources_IncludesDefaultPlusConfigured()
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "OpenTelemetry.ActivitySource.Include", value = "Foo,Bar,Baz" });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            var includedActivitySources = defaultConfig.IncludedActivitySources;

            Assert.That(includedActivitySources, Is.EquivalentTo(["NewRelic.Agent", "Foo", "Bar", "Baz"]));
        }

        [Test]
        public void ExcludedActivitySources_IncludesConfigured()
        {
            _localConfig.appSettings.Add(new configurationAdd { key = "OpenTelemetry.ActivitySource.Exclude", value = "Foo,Bar,Baz" });
            var defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);

            var excludedActivitySources= defaultConfig.ExcludedActivitySources;

            Assert.That(excludedActivitySources, Is.EquivalentTo(["Foo", "Bar", "Baz"]));
        }


        private DefaultConfiguration GenerateConfigFromXml(string xml)
        {
            var root = new XmlRootAttribute { ElementName = "configuration", Namespace = "urn:newrelic-config" };
            var serializer = new XmlSerializer(typeof(configuration), root);

            configuration localConfiguration;
            using (var reader = new StringReader(xml))
            {
                localConfiguration = serializer.Deserialize(reader) as configuration;
            }

            return new TestableDefaultConfiguration(_environment, localConfiguration, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
        }

        private void CreateDefaultConfiguration()
        {
            _defaultConfig = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic, _agentHealthReporter);
        }   
    }
}
