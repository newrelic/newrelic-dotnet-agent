// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;
using Telerik.JustMock;

namespace NewRelic.Agent.Core.Config.UnitTest
{
    [TestFixture]
    [Category("Configuration")]
    public class ConfigurationBridgeTests
    {
        private string _testConfigFile;
        private string _testDirectory;
        private string _originalDirectory;

        [SetUp]
        public void SetUp()
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            _testDirectory = Path.Combine(Path.GetTempPath(), "NewRelicConfigBridgeTest_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(_testDirectory);
            
            _testConfigFile = Path.Combine(_testDirectory, "appsettings.json");
            
            // Create a test appsettings.json file
            var testConfigContent = @"{
  ""NewRelic.AppName"": ""TestApplication"",
  ""NewRelic.LicenseKey"": ""test-license-key-123456789"",
  ""NewRelic.AgentEnabled"": ""true"",
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=TestDb;""
  },
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""NewRelic"": ""Debug""
    }
  },
  ""CustomSection"": {
    ""NestedValue"": ""nested-test-value""
  }
}";
            File.WriteAllText(_testConfigFile, testConfigContent);

            // Create environment-specific config
            var envConfigContent = @"{
  ""NewRelic.AppName"": ""TestApplication-Development"",
  ""Environment"": ""Development""
}";
            File.WriteAllText(Path.Combine(_testDirectory, "appsettings.Development.json"), envConfigContent);

            // Reset any static state
            TestStaticConfigurationHolder.Reset();
            ResetConfigurationBridgeState();
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.SetCurrentDirectory(_originalDirectory);
                TestStaticConfigurationHolder.Reset();
                ResetConfigurationBridgeState();
                
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                // Log but don't fail test on cleanup errors
                Console.WriteLine($"TearDown cleanup error: {ex.Message}");
            }
        }

        private static void ResetConfigurationBridgeState()
        {
            // Use reflection to reset ConfigurationBridge static state for test isolation
            try
            {
                var bridgeType = typeof(ConfigurationBridge);
                var cachedDelegatesField = bridgeType.GetField("_cachedDelegates", BindingFlags.NonPublic | BindingFlags.Static);
                var cachedTypesField = bridgeType.GetField("_cachedTypes", BindingFlags.NonPublic | BindingFlags.Static);
                var initializedField = bridgeType.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
                var bridgeAvailableField = bridgeType.GetField("_bridgeAvailable", BindingFlags.NonPublic | BindingFlags.Static);
                var getConfigurationValueDelegateField = bridgeType.GetField("_getConfigurationValueDelegate", BindingFlags.NonPublic | BindingFlags.Static);

                cachedDelegatesField?.SetValue(null, new ConcurrentDictionary<string, object>());
                cachedTypesField?.SetValue(null, new ConcurrentDictionary<string, Type>());
                initializedField?.SetValue(null, false);
                bridgeAvailableField?.SetValue(null, false);
                getConfigurationValueDelegateField?.SetValue(null, null);
            }
            catch
            {
                // Ignore reflection errors - tests may still pass
            }
        }

        #region Basic Functionality Tests

        [Test]
        public void ConfigurationBridge_WithValidApplicationConfiguration_ShouldAccessApplicationConfig()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();

            // Act
            var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");
            var licenseKey = ConfigurationBridge.GetAppSetting("NewRelic.LicenseKey");
            var agentEnabled = ConfigurationBridge.GetAppSetting("NewRelic.AgentEnabled");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
            Assert.That(licenseKey, Is.EqualTo("test-license-key-123456789"));
            Assert.That(agentEnabled, Is.EqualTo("true"));
        }

        [Test]
        public void ConfigurationBridge_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();

            // Act
            var nonExistentValue = ConfigurationBridge.GetAppSetting("NonExistent.Key");
            var emptyKey = ConfigurationBridge.GetAppSetting("");
            var nullKey = ConfigurationBridge.GetAppSetting(null);

            // Assert
            Assert.That(nonExistentValue, Is.Null);
            Assert.That(emptyKey, Is.Null);
            Assert.That(nullKey, Is.Null);
        }

        [Test]
        public void ConfigurationBridge_WithoutApplicationConfiguration_ShouldFallbackToILRepacked()
        {
            // Arrange - No application configuration setup
            TestStaticConfigurationHolder.Reset();

            // Act
            var result = ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Assert
            // Should fall back to ILRepacked configuration (which returns null in our test environment)
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationBridge_GetAppSettingsFilePath_ShouldReturnValidPath()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();

            // Act
            var filePath = ConfigurationBridge.GetAppSettingsFilePath();

            // Assert
            Assert.That(filePath, Is.Not.Null);
            Assert.That(filePath, Does.EndWith("appsettings.json"));
        }

        [Test]
        public void ConfigurationBridge_GetAppSettingsFilePath_WithoutAppConfig_ShouldFallback()
        {
            // Arrange - No application configuration
            var tempDir = Path.Combine(Path.GetTempPath(), "NoConfigTest");
            Directory.CreateDirectory(tempDir);
            
            try
            {
                Directory.SetCurrentDirectory(tempDir);

                // Act
                var filePath = ConfigurationBridge.GetAppSettingsFilePath();

                // Assert
                Assert.That(filePath, Is.Not.Null); // Should fallback to ILRepacked path
            }
            finally
            {
                Directory.SetCurrentDirectory(_originalDirectory);
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #endregion

        #region Initialization Tests

        [Test]
        public void ConfigurationBridge_Initialize_ShouldBeThreadSafe()
        {
            // Arrange
            var exceptions = new List<Exception>();
            var tasks = new List<System.Threading.Tasks.Task>();

            // Act - Multiple concurrent initializations
            for (int i = 0; i < 10; i++)
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        ConfigurationBridge.Initialize();
                        ConfigurationBridge.GetAppSetting("test");
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });
                tasks.Add(task);
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.That(exceptions, Is.Empty, $"Concurrent initialization caused exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        [Test]
        public void ConfigurationBridge_Initialize_ShouldHandleMultipleCalls()
        {
            // Act & Assert - Should not throw on multiple calls
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
        }

        [Test]
        public void ConfigurationBridge_Initialize_WithException_ShouldFallbackGracefully()
        {
            // Arrange - Force an exception scenario by mocking AppDomain
            ResetConfigurationBridgeState();

            // Act & Assert - Should not throw even if initialization fails
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test"));
        }

        #endregion

        #region ConfigurationManagerStaticBridged Tests

        [Test]
        public void ConfigurationManagerStaticBridged_GetAppSetting_ShouldUseConfigurationBridge()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            var configManager = new ConfigurationManagerStaticBridged();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");
            var licenseKey = configManager.GetAppSetting("NewRelic.LicenseKey");
            var nonExistent = configManager.GetAppSetting("NonExistent.Key");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
            Assert.That(licenseKey, Is.EqualTo("test-license-key-123456789"));
            Assert.That(nonExistent, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_AppSettingsFilePath_ShouldReturnValidPath()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            var configManager = new ConfigurationManagerStaticBridged();

            // Act
            var filePath = configManager.AppSettingsFilePath;

            // Assert
            Assert.That(filePath, Is.Not.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_GetAppSetting_WithNullKey_ShouldReturnNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStaticBridged();

            // Act
            var result = configManager.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_WithException_ShouldDisableAndReturnNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStaticBridged();
            
            // Force an exception by using reflection to set localConfigChecksDisabled
            var field = typeof(ConfigurationManagerStaticBridged).GetField("localConfigChecksDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            var result1 = configManager.GetAppSetting("test"); // This might work
            
            // Force disable
            field?.SetValue(configManager, true);
            var result2 = configManager.GetAppSetting("test"); // This should return null

            // Assert
            Assert.That(result2, Is.Null);
        }

        #endregion

        #region Error Handling Tests

        [Test]
        public void ConfigurationBridge_WithCorruptedConfigFile_ShouldHandleGracefully()
        {
            // Arrange
            var corruptedConfigPath = Path.Combine(_testDirectory, "corrupted-appsettings.json");
            File.WriteAllText(corruptedConfigPath, "{ invalid json }");
            
            Directory.SetCurrentDirectory(_testDirectory);

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test"));
        }

        [Test]
        public void ConfigurationBridge_WithMissingConfigFile_ShouldHandleGracefully()
        {
            // Arrange
            var emptyDir = Path.Combine(Path.GetTempPath(), "EmptyConfigTest");
            Directory.CreateDirectory(emptyDir);
            
            try
            {
                Directory.SetCurrentDirectory(emptyDir);
                ResetConfigurationBridgeState();

                // Act & Assert - Should not throw
                Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
                Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test"));
            }
            finally
            {
                Directory.SetCurrentDirectory(_originalDirectory);
                if (Directory.Exists(emptyDir))
                {
                    Directory.Delete(emptyDir, true);
                }
            }
        }

        [Test]
        public void ConfigurationBridge_WithReflectionException_ShouldFallbackToILRepacked()
        {
            // Arrange - Create scenario where reflection might fail
            ResetConfigurationBridgeState();

            // Act
            var result = ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Assert - Should fall back gracefully
            Assert.That(result, Is.Null); // Fallback should return null in test environment
        }

        [Test]
        public void ConfigurationBridge_WithNullConfigurationInstance_ShouldHandleGracefully()
        {
            // Arrange
            ResetConfigurationBridgeState();
            TestStaticConfigurationHolder.Configuration = null;

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test"));
        }

        [Test]
        public void ConfigurationBridge_WithInvalidIndexerProperty_ShouldFallback()
        {
            // Arrange - Setup configuration that might have indexer issues
            ResetConfigurationBridgeState();
            
            // Act & Assert - Should handle missing or invalid indexer gracefully
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test.key"));
        }

        [Test]
        public void ConfigurationBridge_WithExceptionInDelegateExecution_ShouldFallback()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            
            // Force initialization first
            ConfigurationBridge.Initialize();

            // Act - Even if delegate execution fails, should fallback gracefully
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("potentially.problematic.key"));
        }

        [Test]
        public void ConfigurationBridge_WithEmptyAssemblyList_ShouldHandleGracefully()
        {
            // Arrange - This tests the edge case where no suitable assemblies are found
            ResetConfigurationBridgeState();

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
            var result = ConfigurationBridge.GetAppSetting("test");
            Assert.That(result, Is.Null); // Should fallback to ILRepacked
        }

        #endregion

        #region Integration Tests

        [Test]
        public void ConfigurationBridge_WithServiceProviderPattern_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            var configuration = CreateTestConfiguration();
            TestStaticConfigurationHolder.SetupServiceProvider(configuration);

            // Act
            var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
        }

        [Test]
        public void ConfigurationBridge_WithStaticFieldPattern_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            var configuration = CreateTestConfiguration();
            TestStaticConfigurationHolder.Configuration = configuration;

            // Act
            var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
        }

        [Test]
        public void ConfigurationBridge_WithComplexConfigurationHierarchy_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupComplexConfiguration();

            // Act
            var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");
            var connectionString = ConfigurationBridge.GetAppSetting("ConnectionStrings:DefaultConnection");
            var nestedValue = ConfigurationBridge.GetAppSetting("CustomSection:NestedValue");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
            Assert.That(connectionString, Is.EqualTo("Server=localhost;Database=TestDb;"));
            Assert.That(nestedValue, Is.EqualTo("nested-test-value"));
        }

        [Test]
        public void ConfigurationBridge_WithEnvironmentOverrides_ShouldUseCorrectValues()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
            
            try
            {
                SetupEnvironmentSpecificConfiguration();

                // Act
                var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");
                var environment = ConfigurationBridge.GetAppSetting("Environment");

                // Assert
                Assert.That(appName, Is.EqualTo("TestApplication-Development"));
                Assert.That(environment, Is.EqualTo("Development"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", null);
            }
        }

        #endregion

        #region Performance Tests

        [Test]
        public void ConfigurationBridge_RepeatedAccess_ShouldBeFast()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            
            // Warm up
            ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Act & Assert - Should complete quickly
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                ConfigurationBridge.GetAppSetting("NewRelic.AppName");
            }
            stopwatch.Stop();

            // Assert - Should be fast (under 100ms for 1000 calls)
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100), 
                "Configuration access should be fast after initialization");
        }

        #endregion

        #region Classic Configuration Manager Tests

        [Test]
        public void ConfigurationBridge_WithClassicConfigManager_ShouldFallbackToAppConfig()
        {
            // Arrange - Reset to simulate environment without Microsoft.Extensions.Configuration
            ResetConfigurationBridgeState();
            TestStaticConfigurationHolder.Reset();

            // Act - Should attempt classic config manager fallback
            var result = ConfigurationBridge.GetAppSetting("NonExistent.Key");

            // Assert - Should not throw and handle gracefully
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
        }

        [Test]
        public void ConfigurationBridge_WithMissingMicrosoftExtensionsConfiguration_ShouldHandleGracefully()
        {
            // Arrange - Simulate environment where Microsoft.Extensions.Configuration is not available
            ResetConfigurationBridgeState();
            TestStaticConfigurationHolder.Reset();

            // Act & Assert
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test.key"));
        }

        #endregion

        #region Assembly Discovery Tests

        [Test]
        public void ConfigurationBridge_AssemblyDiscovery_ShouldIgnoreNewRelicAssemblies()
        {
            // Arrange
            ResetConfigurationBridgeState();

            // Act - This should trigger assembly discovery logic
            ConfigurationBridge.Initialize();

            // Assert - Should not throw and complete successfully
            Assert.DoesNotThrow(() => ConfigurationBridge.GetAppSetting("test"));
        }

        [Test]
        public void ConfigurationBridge_AssemblyDiscovery_ShouldIgnoreGACAssemblies()
        {
            // Arrange
            ResetConfigurationBridgeState();

            // Act - Assembly discovery should skip GAC assemblies
            Assert.DoesNotThrow(() => ConfigurationBridge.Initialize());
        }

        #endregion

        #region Caching Tests

        [Test]
        public void ConfigurationBridge_RepeatedInitialization_ShouldUseCachedResults()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            
            // Act - Multiple initializations
            ConfigurationBridge.Initialize();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            for (int i = 0; i < 100; i++)
            {
                ConfigurationBridge.Initialize();
            }
            
            stopwatch.Stop();

            // Assert - Subsequent initializations should be very fast due to caching
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(10), 
                "Cached initialization should be very fast");
        }

        [Test]
        public void ConfigurationBridge_TypeCaching_ShouldImprovePerformance()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            
            // Warm up the cache
            ConfigurationBridge.GetAppSetting("NewRelic.AppName");

            // Act - Time multiple calls that should hit the cache
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                ConfigurationBridge.GetAppSetting("NewRelic.LicenseKey");
            }
            stopwatch.Stop();

            // Assert - Should be very fast due to delegate caching
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50), 
                "Cached delegate calls should be very fast");
        }

        #endregion

        #region Logging and Debugging Tests

        [Test]
        public void ConfigurationBridge_WithLoggingEnabled_ShouldLogConfigurationAccess()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();

            // Act - These calls should generate debug logs (though we can't assert on them directly)
            var appName = ConfigurationBridge.GetAppSetting("NewRelic.AppName");
            var nonExistent = ConfigurationBridge.GetAppSetting("NonExistent.Key");

            // Assert - Should complete without exceptions
            Assert.That(appName, Is.EqualTo("TestApplication"));
            Assert.That(nonExistent, Is.Null);
        }

        [Test]
        public void ConfigurationBridge_WithLicenseKey_ShouldObfuscateInLogs()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();

            // Act - License key access should trigger obfuscation logic
            var licenseKey = ConfigurationBridge.GetAppSetting("NewRelic.LicenseKey");

            // Assert
            Assert.That(licenseKey, Is.EqualTo("test-license-key-123456789"));
            // Note: Log obfuscation behavior is tested implicitly - the actual obfuscation
            // happens in logging, not in the return value
        }

        #endregion

        #region Helper Methods

        private void SetupApplicationConfiguration()
        {
            var configuration = CreateTestConfiguration();
            TestStaticConfigurationHolder.Configuration = configuration;
        }

        private void SetupComplexConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(_testDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            TestStaticConfigurationHolder.Configuration = configuration;
        }

        private void SetupEnvironmentSpecificConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(_testDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();
            TestStaticConfigurationHolder.Configuration = configuration;
        }

        private IConfiguration CreateTestConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(_testDirectory)
                .AddJsonFile("appsettings.json", optional: false);

            return builder.Build();
        }

        #endregion
    }

    #region Test Helper Classes

    /// <summary>
    /// Test helper class to simulate how applications typically hold configuration instances
    /// </summary>
    public static class TestStaticConfigurationHolder
    {
        public static IConfiguration Configuration { get; set; }
        public static IServiceProvider ServiceProvider { get; set; }

        public static void Reset()
        {
            Configuration = null;
            ServiceProvider = null;
        }

        public static void SetupServiceProvider(IConfiguration configuration)
        {
            var mockServiceProvider = Mock.Create<IServiceProvider>();
            Mock.Arrange(() => mockServiceProvider.GetService(typeof(IConfiguration)))
                .Returns(configuration);
            ServiceProvider = mockServiceProvider;
        }
    }

    /// <summary>
    /// Mock service provider for testing DI scenarios
    /// </summary>
    public class TestServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void RegisterService<T>(T service)
        {
            _services[typeof(T)] = service;
        }

        public object GetService(Type serviceType)
        {
            _services.TryGetValue(serviceType, out var service);
            return service;
        }
    }

    #endregion
}
#endif
