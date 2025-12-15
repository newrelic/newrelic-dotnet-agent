// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.Core.Config;
using NewRelic.Agent.Core.SharedInterfaces;
using NewRelic.Agent.Core.SharedInterfaces.Web;
using NewRelic.Testing.Assertions;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Configuration
{
    [TestFixture]
    public class OpenTelemetryConfigurationTests
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
        private TestableDefaultConfiguration _configuration;
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
            _bootstrapConfiguration = Mock.Create<IBootstrapConfiguration>();
            _dnsStatic = Mock.Create<IDnsStatic>();

            _configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
        }

        [Test]
        public void OpenTelemetryEnabled_DefaultValue_ShouldBeFalse()
        {
            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(_configuration.OpenTelemetryEnabled, Is.False),
                () => Assert.That(_configuration.OpenTelemetryMetricsEnabled, Is.False)
            );
        }

        #region Metrics Tests
        [Test]
        public void OpenTelemetryMetricsEnabled_RequiresBothGlobalAndMetricsSettings_ToBeTrue()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(configuration.OpenTelemetryEnabled, Is.True),
                () => Assert.That(configuration.OpenTelemetryMetricsEnabled, Is.True)
            );
        }

        [Test]
        public void OpenTelemetryMetricsEnabled_WithOnlyGlobalSettingTrue_ShouldBeFalse()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = false // metrics specific setting is false
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(configuration.OpenTelemetryEnabled, Is.True),
                () => Assert.That(configuration.OpenTelemetryMetricsEnabled, Is.False)
            );
        }

        [Test]
        public void OpenTelemetryMetricsIncludeFilters_ShouldParseCommaSeparatedValues()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true,
                    include = "MeterName1,MeterName2, MeterName3 "
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act
            var includeFilters = configuration.OpenTelemetryMetricsIncludeFilters.ToList();

            // Assert
            NrAssert.Multiple(
                () => Assert.That(includeFilters, Has.Count.EqualTo(3)),
                () => Assert.That(includeFilters, Contains.Item("MeterName1")),
                () => Assert.That(includeFilters, Contains.Item("MeterName2")),
                () => Assert.That(includeFilters, Contains.Item("MeterName3"))
            );
        }

        [Test]
        public void OpenTelemetryMetricsExcludeFilters_ShouldParseCommaSeparatedValues()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true,
                    exclude = "Debug.Meter,Test.Meter"
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act
            var excludeFilters = configuration.OpenTelemetryMetricsExcludeFilters.ToList();

            // Assert
            NrAssert.Multiple(
                () => Assert.That(excludeFilters, Has.Count.EqualTo(2)),
                () => Assert.That(excludeFilters, Contains.Item("Debug.Meter")),
                () => Assert.That(excludeFilters, Contains.Item("Test.Meter"))
            );
        }

        [Test]
        public void OpenTelemetryMetricsFilters_WithEmptyStrings_ShouldReturnEmptyCollections()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                metrics = new configurationOpentelemetryMetrics
                {
                    enabled = true,
                    include = "",
                    exclude = ""
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(configuration.OpenTelemetryMetricsIncludeFilters, Is.Empty),
                () => Assert.That(configuration.OpenTelemetryMetricsExcludeFilters, Is.Empty)
            );
        }
        #endregion

        #region Tracing tests
        [Test]
        public void OpenTelemetryTracesEnabled_RequiresBothGlobalAndTracesSettings_ToBeTrue()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                traces = new configurationOpentelemetryTraces
                {
                    enabled = true
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(configuration.OpenTelemetryEnabled, Is.True),
                () => Assert.That(configuration.OpenTelemetryTracingEnabled, Is.True)
            );
        }

        [Test]
        public void OpenTelemetryTracesEnabled_WithOnlyGlobalSettingTrue_ShouldBeFalse()
        {
            // Arrange
            _localConfig.opentelemetry = new configurationOpentelemetry
            {
                enabled = true,
                traces = new configurationOpentelemetryTraces
                {
                    enabled = false // traces specific setting is false
                }
            };

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            // Act & Assert
            NrAssert.Multiple(
                () => Assert.That(configuration.OpenTelemetryEnabled, Is.True),
                () => Assert.That(configuration.OpenTelemetryTracingEnabled, Is.False)
            );
        }

        [Test]
        public void OpenTelemetryTracingIncludedActivitySources_ParsesPoorlyFormedLocalString()
        {
            _localConfig.opentelemetry.traces.include = "  Foo , ,Bar,,  baz , Foo  ";

            var configuration = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            var result = configuration.OpenTelemetryTracingIncludedActivitySources;

            Assert.That(result, Is.EqualTo(new List<string> { "Foo", "Bar", "baz" }));
        }

        [Test]
        public void OpenTelemetryTracingIncludedActivitySources_EnvironmentOverridesLocal_AndParses()
        {
            _localConfig.opentelemetry.traces.include = "LocalA,LocalB";
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_OPENTELEMETRY_TRACES_INCLUDE"))
                .Returns(" A , , B , C , A ");

            var cfg = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            var result = cfg.OpenTelemetryTracingIncludedActivitySources;

            Assert.That(result, Is.EqualTo(new List<string> { "A", "B", "C" }));
        }

        [Test]
        public void OpenTelemetryTracingIncludedActivitySources_EmptyOrWhitespaceTokens_AreIgnored()
        {
            _localConfig.opentelemetry.traces.include = " , ,   , ";
            var cfg = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.That(cfg.OpenTelemetryTracingIncludedActivitySources, Is.Empty);
        }

        [Test]
        public void OpenTelemetryTracingExcludedActivitySources_ParsesPoorlyFormedLocalString()
        {
            _localConfig.opentelemetry.traces.exclude = "  Ex1 , , Ex2,,  Ex3 , Ex1  ";

            var cfg = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            var result = cfg.OpenTelemetryTracingExcludedActivitySources;

            Assert.That(result, Is.EqualTo(new List<string> { "Ex1", "Ex2", "Ex3" }));
        }

        [Test]
        public void OpenTelemetryTracingExcludedActivitySources_EnvironmentOverridesLocal_AndParses()
        {
            _localConfig.opentelemetry.traces.exclude = "LocalX,LocalY";
            Mock.Arrange(() => _environment.GetEnvironmentVariableFromList("NEW_RELIC_OPENTELEMETRY_TRACES_EXCLUDE"))
                .Returns(" X , , Y , Z , X ");

            var cfg = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);
            var result = cfg.OpenTelemetryTracingExcludedActivitySources;

            Assert.That(result, Is.EqualTo(new List<string> { "X", "Y", "Z" }));
        }

        [Test]
        public void OpenTelemetryTracingExcludedActivitySources_EmptyOrWhitespaceTokens_AreIgnored()
        {
            _localConfig.opentelemetry.traces.exclude = " , ,   , ";
            var cfg = new TestableDefaultConfiguration(_environment, _localConfig, _serverConfig, _runTimeConfig, _securityPoliciesConfiguration, _bootstrapConfiguration, _processStatic, _httpRuntimeStatic, _configurationManagerStatic, _dnsStatic);

            Assert.That(cfg.OpenTelemetryTracingExcludedActivitySources, Is.Empty);
        }
        #endregion
    }
}
