// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;
using Telerik.JustMock;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Agent.Core.AgentHealth;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture, Category("Configuration")]
    public class DefaultConfiguration_SamplerConfigurationTests
    {
        private IEnvironment _environment;
        private configuration _localConfig;
        private ServerConfiguration _serverConfig;
        private RunTimeConfiguration _runTimeConfig;
        private SecurityPoliciesConfiguration _securityPoliciesConfiguration;
        private IBootstrapConfiguration _bootstrapConfiguration;
        private IProcessStatic _processStatic;
        private IHttpRuntimeStatic _httpRuntimeStatic;
        private IConfigurationManagerStatic _configurationManagerStatic;
        private IDnsStatic _dnsStatic;
        private IAgentHealthReporter _agentHealthReporter;

        private DefaultConfiguration _config;

        [SetUp]
        public void SetUp()
        {
            _environment = Mock.Create<IEnvironment>();
            _localConfig = new configuration();
            _serverConfig = new ServerConfiguration();
            _runTimeConfig = new RunTimeConfiguration();
            _securityPoliciesConfiguration = new SecurityPoliciesConfiguration();
            _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
            _processStatic = Mock.Create<IProcessStatic>();
            _httpRuntimeStatic = Mock.Create<IHttpRuntimeStatic>();
            _configurationManagerStatic = Mock.Create<IConfigurationManagerStatic>();
            _dnsStatic = Mock.Create<IDnsStatic>();
            _agentHealthReporter = Mock.Create<IAgentHealthReporter>();

            // Default: distributed tracing enabled so sampler logic is relevant
            _localConfig.distributedTracing.enabled = true;

            CreateConfig();
        }

        private void CreateConfig()
        {
            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);
        }

        [TestCase("alwaysOn", SamplerType.AlwaysOn, TestName = "RootSamplerType_AlwaysOn_EnvironmentVariableOverride")]
        [TestCase("alwaysOff", SamplerType.AlwaysOff, TestName = "RootSamplerType_AlwaysOff_EnvironmentVariableOverride")]
        [TestCase("default", SamplerType.Adaptive, TestName = "RootSamplerType_Default_EnvironmentVariableOverride")]
        [TestCase("adaptive", SamplerType.Adaptive, TestName = "RootSamplerType_Adaptive_EnvironmentVariableOverride")]
        [TestCase("traceIdRatioBased", SamplerType.TraceIdRatioBased, TestName = "RootSamplerType_TraceIdRatioBased_EnvironmentVariableOverride")]
        [TestCase("invalidValue", SamplerType.Adaptive, TestName = "RootSamplerType_InvalidValueDefaultsToDefault_EnvironmentVariableOverride")]
        public void RootSampler_UsesEnvironmentVariableOverride(string environmentVariableValue, SamplerType expectedSamplerType)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns(environmentVariableValue);

            // Act
            var result = _config.RootSamplerType;

            // Assert
            Assert.That(result, Is.EqualTo(expectedSamplerType));
        }


        [TestCase("alwaysOn", SamplerType.AlwaysOn, TestName = "RemoteParentSampledSamplerType_AlwaysOn_EnvironmentVariableOverride")]
        [TestCase("AlWaYSOn", SamplerType.AlwaysOn, TestName = "RemoteParentSampledSamplerType_AlwaysOnMixedCase_EnvironmentVariableOverride")]
        [TestCase("alwaysOff", SamplerType.AlwaysOff, TestName = "RemoteParentSampledSamplerType_AlwaysOff_EnvironmentVariableOverride")]
        [TestCase("traceIdRatioBased", SamplerType.TraceIdRatioBased, TestName = "RemoteParentSampledSamplerType_TraceIdRatioBased_EnvironmentVariableOverride")]
        [TestCase("default", SamplerType.Adaptive, TestName = "RemoteParentSampledSamplerType_Default_EnvironmentVariableOverride")]
        [TestCase("adaptive", SamplerType.Adaptive, TestName = "RemoteParentSampledSamplerType_Adaptive_EnvironmentVariableOverride")]
        [TestCase("invalidValue", SamplerType.Adaptive, TestName = "RemoteParentSampledSamplerType_InvalidValueDefaultsToDefault_EnvironmentVariableOverride")]
        public void RemoteParentSampledSamplerType_UsesEnvironmentVariableOverride(string environmentVariableValue, SamplerType expectedRemoteParentSampledSamplerType)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns(environmentVariableValue);

            // Act
            var result = _config.RemoteParentSampledSamplerType;

            // Assert
            Assert.That(result, Is.EqualTo(expectedRemoteParentSampledSamplerType));
        }

        [TestCase("alwaysOn", SamplerType.AlwaysOn, TestName = "RemoteParentNotSampledSamplerType_AlwaysOn_EnvironmentVariableOverride")]
        [TestCase("alwaysOff", SamplerType.AlwaysOff, TestName = "RemoteParentNotSampledSamplerType_AlwaysOff_EnvironmentVariableOverride")]
        [TestCase("default", SamplerType.Adaptive, TestName = "RemoteParentNotSampledSamplerType_Default_EnvironmentVariableOverride")]
        [TestCase("adaptive", SamplerType.Adaptive, TestName = "RemoteParentNotSampledSamplerType_Adaptive_EnvironmentVariableOverride")]
        [TestCase("traceIdRatioBased", SamplerType.TraceIdRatioBased, TestName = "RemoteParentNotSampledSamplerType_TraceIdRatioBased_EnvironmentVariableOverride")]
        [TestCase("invalidValue", SamplerType.Adaptive, TestName = "RemoteParentNotSampledSamplerType_InvalidValueDefaultsToDefault_EnvironmentVariableOverride")]
        public void RemoteParentNotSampledSamplerType_UsesEnvironmentVariableOverride(string environmentVariableValue, SamplerType expectedRemoteParentSampledSamplerType)
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED"))
                .Returns(environmentVariableValue);

            // Act
            var result = _config.RemoteParentNotSampledSamplerType;

            // Assert
            Assert.That(result, Is.EqualTo(expectedRemoteParentSampledSamplerType));
        }

        #region RootTraceIdRatioSamplerRatio Tests

        [Test]
        public void RootTraceIdRatioSamplerRatio_ReturnsConfiguredRatio_WhenSamplerTypeIsTraceIdRatioBased()
        {
            // Arrange
            var ratio = 0.42m;
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = ratio };

            // Act
            var value = _config.RootTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo((float)ratio));
        }

        [TestCase(SamplerType.AlwaysOn)]
        [TestCase(SamplerType.AlwaysOff)]
        [TestCase(SamplerType.Adaptive)]
        public void RootTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsNotTraceIdRatioBased(SamplerType samplerType)
        {
            // Arrange
            _localConfig.distributedTracing.sampler.root.Item = samplerType switch
            {
                SamplerType.AlwaysOn => new AlwaysOnSamplerType(),
                SamplerType.AlwaysOff => new AlwaysOffSamplerType(),
                _ => new AdaptiveSamplerType()
            };

            // Act
            var value = _config.RootTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RootTraceIdRatioSamplerRatio_UsesLocalRatio_WhenEnvOverridesSamplerTypeToTraceIdRatioBased()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = 0.7m };

            // Recreate config to apply env override
            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RootTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo(0.7f));
        }

        [Test]
        public void RootTraceIdRatioSamplerRatio_IsNull_WhenEnvOverridesSamplerTypeToNonRatioBased()
        {
            // Arrange
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = 0.15m }; // should be ignored
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("alwaysOn");

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RootTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RootTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsTraceIdRatioBasedButNoRatioConfigured()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.root.Item = new AdaptiveSamplerType(); // no TraceIdRatioSamplerType provided

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RootTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        #endregion RootTraceIdRatioSamplerRatio Tests

        #region RemoteParentSampledTraceIdRatioSamplerRatio Tests

        [Test]
        public void RemoteParentSampledTraceIdRatioSamplerRatio_ReturnsConfiguredRatio_WhenSamplerTypeIsTraceIdRatioBased()
        {
            // Arrange
            var ratio = 0.33m;
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new TraceIdRatioBasedSamplerType { ratio = ratio };

            // Act
            var value = _config.RemoteParentSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo((float)ratio));
        }

        [TestCase(SamplerType.AlwaysOn)]
        [TestCase(SamplerType.AlwaysOff)]
        [TestCase(SamplerType.Adaptive)]
        public void RemoteParentSampledTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsNotTraceIdRatioBased(SamplerType samplerType)
        {
            // Arrange
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = samplerType switch
            {
                SamplerType.AlwaysOn => new AlwaysOnSamplerType(),
                SamplerType.AlwaysOff => new AlwaysOffSamplerType(),
                _ => new AdaptiveSamplerType()
            };

            // Act
            var value = _config.RemoteParentSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RemoteParentSampledTraceIdRatioSamplerRatio_UsesLocalRatio_WhenEnvOverridesSamplerTypeToTraceIdRatioBased()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.55m };

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo(0.55f));
        }

        [Test]
        public void RemoteParentSampledTraceIdRatioSamplerRatio_IsNull_WhenEnvOverridesSamplerTypeToNonRatioBased()
        {
            // Arrange
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.25m }; // should be ignored
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns("alwaysOff");

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RemoteParentSampledTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsTraceIdRatioBasedButNoRatioConfigured()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new AdaptiveSamplerType(); // no ratio sampler object

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        #endregion RemoteParentSampledTraceIdRatioSamplerRatio Tests

        #region RemoteParentNotSampledTraceIdRatioSamplerRatio Tests

        [Test]
        public void RemoteParentNotSampledTraceIdRatioSamplerRatio_ReturnsConfiguredRatio_WhenSamplerTypeIsTraceIdRatioBased()
        {
            // Arrange
            var ratio = 0.61m;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new TraceIdRatioBasedSamplerType { ratio = ratio };

            // Act
            var value = _config.RemoteParentNotSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo((float)ratio));
        }

        [TestCase(SamplerType.AlwaysOn)]
        [TestCase(SamplerType.AlwaysOff)]
        [TestCase(SamplerType.Adaptive)]
        public void RemoteParentNotSampledTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsNotTraceIdRatioBased(SamplerType samplerType)
        {
            // Arrange
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = samplerType switch
            {
                SamplerType.AlwaysOn => new AlwaysOnSamplerType(),
                SamplerType.AlwaysOff => new AlwaysOffSamplerType(),
                _ => new AdaptiveSamplerType()
            };

            // Act
            var value = _config.RemoteParentNotSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RemoteParentNotSampledTraceIdRatioSamplerRatio_UsesLocalRatio_WhenEnvOverridesSamplerTypeToTraceIdRatioBased()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.9m };

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentNotSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.EqualTo(0.9f));
        }

        [Test]
        public void RemoteParentNotSampledTraceIdRatioSamplerRatio_IsNull_WhenEnvOverridesSamplerTypeToNonRatioBased()
        {
            // Arrange
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.11m }; // should be ignored
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED"))
                .Returns("default");

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentNotSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        [Test]
        public void RemoteParentNotSampledTraceIdRatioSamplerRatio_ReturnsNull_WhenSamplerTypeIsTraceIdRatioBasedButNoRatioConfigured()
        {
            // Arrange
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_NOT_SAMPLED"))
                .Returns("traceIdRatioBased");
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new AdaptiveSamplerType(); // no TraceIdRatioSamplerType

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act
            var value = _config.RemoteParentNotSampledTraceIdRatioSamplerRatio;

            // Assert
            Assert.That(value, Is.Null);
        }

        #endregion RemoteParentNotSampledTraceIdRatioSamplerRatio Tests

        #region SamplerType legacy fallback tests

        [Test]
        public void RemoteParentSampledSamplerType_UsesLegacyFallback_WhenItemNull_AlwaysOn()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = null; // force legacy path
            _localConfig.distributedTracing.sampler.remoteParentSampled1 = RemoteParentSampledBehaviorType.alwaysOn;

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentSampledSamplerType, Is.EqualTo(SamplerType.AlwaysOn));
        }

        [Test]
        public void RemoteParentSampledSamplerType_UsesLegacyFallback_WhenItemNull_TraceIdRatioBased()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = null;
            _localConfig.distributedTracing.sampler.remoteParentSampled1 = RemoteParentSampledBehaviorType.traceIdRatioBased;

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentSampledSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
        }

        [Test]
        public void RemoteParentSampledSamplerType_IgnoresLegacy_WhenItemPresent()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new AlwaysOffSamplerType();
            _localConfig.distributedTracing.sampler.remoteParentSampled1 = RemoteParentSampledBehaviorType.alwaysOn; // should be ignored

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentSampledSamplerType, Is.EqualTo(SamplerType.AlwaysOff));
        }

        [Test]
        public void RemoteParentNotSampledSamplerType_UsesLegacyFallback_WhenItemNull_AlwaysOff()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = null;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled1 = RemoteParentSampledBehaviorType.alwaysOff;

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentNotSampledSamplerType, Is.EqualTo(SamplerType.AlwaysOff));
        }

        [Test]
        public void RemoteParentNotSampledSamplerType_UsesLegacyFallback_WhenItemNull_Default()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = null;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled1 = RemoteParentSampledBehaviorType.@default; // should map to Default

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentNotSampledSamplerType, Is.EqualTo(SamplerType.Adaptive));
        }

        [Test]
        public void RemoteParentNotSampledSamplerType_IgnoresLegacy_WhenItemPresent()
        {
            // Arrange
            _localConfig.distributedTracing.enabled = true;
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new AlwaysOnSamplerType();
            _localConfig.distributedTracing.sampler.remoteParentNotSampled1 = RemoteParentSampledBehaviorType.alwaysOff; // should be ignored

            _config = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig,
                _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic,
                _configurationManagerStatic, _dnsStatic);

            // Act / Assert
            Assert.That(_config.RemoteParentNotSampledSamplerType, Is.EqualTo(SamplerType.AlwaysOn));
        }

        #endregion


        [Test]
        public void RootSampler_Ratio_EnvironmentOverride_TakesPrecedence()
        {
            // local config ratio (should be overridden)
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = 0.33m };

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("traceIdRatioBased");
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT_TRACE_ID_RATIO_BASED_RATIO"))
                .Returns("0.75");

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RootSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
                Assert.That(_config.RootTraceIdRatioSamplerRatio, Is.EqualTo(0.75f));
            });
        }

        [Test]
        public void RootSampler_Ratio_InvalidEnv_FallsBackToLocal()
        {
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = 0.25m };

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("traceIdRatioBased");
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT_TRACE_ID_RATIO_BASED_RATIO"))
                .Returns("abc"); // invalid

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RootSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
                Assert.That(_config.RootTraceIdRatioSamplerRatio, Is.EqualTo(0.25f));
            });
        }

        [Test]
        public void RootSampler_Ratio_EnvSamplerNotRatioBased_ReturnsNull()
        {
            _localConfig.distributedTracing.sampler.root.Item = new TraceIdRatioBasedSamplerType { ratio = 0.20m };

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT"))
                .Returns("alwaysOn");
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_ROOT_TRACE_ID_RATIO_BASED_RATIO"))
                .Returns("0.90");

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RootSamplerType, Is.EqualTo(SamplerType.AlwaysOn));
                Assert.That(_config.RootTraceIdRatioSamplerRatio, Is.Null);
            });
        }

        [Test]
        public void RemoteParentSampled_Ratio_EnvironmentOverride_TakesPrecedence()
        {
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.10m };

            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED"))
                .Returns("traceIdRatioBased");
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_DISTRIBUTED_TRACING_SAMPLER_REMOTE_PARENT_SAMPLED_TRACE_ID_RATIO_BASED_RATIO"))
                .Returns("0.55");

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RemoteParentSampledSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
                Assert.That(_config.RemoteParentSampledTraceIdRatioSamplerRatio, Is.EqualTo(0.55f));
            });
        }

        [Test]
        public void RemoteParentNotSampled_Ratio_LocalOnly()
        {
            _localConfig.distributedTracing.sampler.remoteParentNotSampled.Item = new TraceIdRatioBasedSamplerType { ratio = 0.60m };

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RemoteParentNotSampledSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
                Assert.That(_config.RemoteParentNotSampledTraceIdRatioSamplerRatio, Is.EqualTo(0.60f));
            });
        }

        [Test]
        public void RemoteParentSampled_LegacyFallback_TraceIdRatioBased_NoRatioConfigured()
        {
            // Simulate legacy usage: Item null, legacy enum set
            _localConfig.distributedTracing.sampler.remoteParentSampled.Item = null;
            _localConfig.distributedTracing.sampler.remoteParentSampled1 = RemoteParentSampledBehaviorType.traceIdRatioBased;

            CreateConfig();

            Assert.Multiple(() =>
            {
                Assert.That(_config.RemoteParentSampledSamplerType, Is.EqualTo(SamplerType.TraceIdRatioBased));
                Assert.That(_config.RemoteParentSampledTraceIdRatioSamplerRatio, Is.Null);
            });
        }

        #region EventListenerSamplersEnabled Tests

        [Test]
        public void EventListenerSamplersEnabled_DefaultsToTrue()
        {
            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void EventListenerSamplersEnabled_ReturnsFalse_WhenOpenTelemetryMetricsEnabled()
        {
            // Arrange
            _localConfig.openTelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true
                }
            };

            CreateConfig();

            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void EventListenerSamplersEnabled_ReturnsTrue_WhenOpenTelemetryMetricsDisabled()
        {
            // Arrange
            _localConfig.openTelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = false
                }
            };

            CreateConfig();

            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert
            Assert.That(result, Is.True);
        }

        [Test]
        public void EventListenerSamplersEnabled_CanBeExplicitlyDisabledViaAppSetting()
        {
            // Arrange
            _localConfig.appSettings.Add(new configurationAdd { key = "NewRelic.EventListenerSamplersEnabled", value = "false" });

            CreateConfig();

            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert
            Assert.That(result, Is.False);
        }

        [Test]
        public void EventListenerSamplersEnabled_AppSettingOverriddenByOpenTelemetryMetrics()
        {
            // Arrange - explicitly enable EventListener samplers via app setting
            _localConfig.appSettings.Add(new configurationAdd { key = "NewRelic.EventListenerSamplersEnabled", value = "true" });
            // But also enable OpenTelemetry metrics (should take precedence)
            _localConfig.openTelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true
                }
            };

            CreateConfig();

            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert - OpenTelemetry metrics setting should override explicit app setting
            Assert.That(result, Is.False);
        }

        [Test]
        public void EventListenerSamplersEnabled_CanBeExplicitlyEnabledViaAppSetting()
        {
            // Arrange
            _localConfig.appSettings.Add(new configurationAdd { key = "NewRelic.EventListenerSamplersEnabled", value = "true" });
            _localConfig.openTelemetry = new configurationOpentelemetry
            {
                enabled = false,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = false
                }
            };

            CreateConfig();

            // Act
            var result = _config.EventListenerSamplersEnabled;

            // Assert
            Assert.That(result, Is.True);
        }

        #endregion
    }
}
