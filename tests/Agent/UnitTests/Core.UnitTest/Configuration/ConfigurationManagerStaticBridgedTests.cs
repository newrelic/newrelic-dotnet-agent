// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.IO;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;

#if NETSTANDARD2_0
using Microsoft.Extensions.Configuration;
#endif

namespace NewRelic.Agent.Core.Config.UnitTest
{
    [TestFixture]
    [Category("Configuration")]
    public class ConfigurationManagerStaticBridgedTests
    {
        private string _testDirectory;
        private string _originalDirectory;
        private ConfigurationManagerStaticBridged _configManager;

        [SetUp]
        public void SetUp()
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            _testDirectory = Path.Combine(Path.GetTempPath(), "NewRelicConfigManagerBridgedTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(_testDirectory);

            _configManager = new ConfigurationManagerStaticBridged();

#if NETSTANDARD2_0
            // Reset test helper state
            TestStaticConfigurationHolder.Reset();
            ResetConfigurationBridgeState();
#endif
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.SetCurrentDirectory(_originalDirectory);

#if NETSTANDARD2_0
                TestStaticConfigurationHolder.Reset();
                ResetConfigurationBridgeState();
#endif

                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"TearDown cleanup error: {ex.Message}");
            }
        }

#if NETSTANDARD2_0
        private static void ResetConfigurationBridgeState()
        {
            try
            {
                var bridgeType = typeof(ConfigurationBridge);
                var cachedDelegatesField = bridgeType.GetField("_cachedDelegates", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var cachedTypesField = bridgeType.GetField("_cachedTypes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var initializedField = bridgeType.GetField("_initialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var bridgeAvailableField = bridgeType.GetField("_bridgeAvailable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                var getConfigurationValueDelegateField = bridgeType.GetField("_getConfigurationValueDelegate", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

                cachedDelegatesField?.SetValue(null, new System.Collections.Concurrent.ConcurrentDictionary<string, object>());
                cachedTypesField?.SetValue(null, new System.Collections.Concurrent.ConcurrentDictionary<string, Type>());
                initializedField?.SetValue(null, false);
                bridgeAvailableField?.SetValue(null, false);
                getConfigurationValueDelegateField?.SetValue(null, null);
            }
            catch
            {
                // Ignore reflection errors
            }
        }
#endif

        #region .NET Framework Tests

#if NETFRAMEWORK
        [Test]
        public void ConfigurationManagerStaticBridged_NetFramework_GetAppSetting_ShouldUseSystemConfiguration()
        {
            // Arrange - In .NET Framework, should use System.Configuration.ConfigurationManager

            // Act
            var result = _configManager.GetAppSetting("test.key");

            // Assert
            // In test environment, System.Configuration.ConfigurationManager.AppSettings returns null for non-existent keys
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetFramework_GetAppSetting_WithNullKey_ShouldReturnNull()
        {
            // Act
            var result = _configManager.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetFramework_AppSettingsFilePath_ShouldReturnConfigPath()
        {
            // Act
            var filePath = _configManager.AppSettingsFilePath;

            // Assert
            Assert.That(filePath, Is.Not.Null);
            // In .NET Framework, this should typically end with .config
            Assert.That(filePath, Does.EndWith(".config").Or.EndWith(".exe.config"));
        }
#endif

        #endregion

        #region .NET Standard Tests

#if NETSTANDARD2_0
        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_GetAppSetting_WithConfigurationBridge_ShouldReturnValue()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();

            // Act
            var appName = _configManager.GetAppSetting("NewRelic.AppName");
            var licenseKey = _configManager.GetAppSetting("NewRelic.LicenseKey");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
            Assert.That(licenseKey, Is.EqualTo("test-license-key-123"));
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_GetAppSetting_WithNonExistentKey_ShouldReturnNull()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();

            // Act
            var result = _configManager.GetAppSetting("NonExistent.Key");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_GetAppSetting_WithNullKey_ShouldReturnNull()
        {
            // Act
            var result = _configManager.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_GetAppSetting_WithEmptyKey_ShouldReturnNull()
        {
            // Act
            var result = _configManager.GetAppSetting("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_AppSettingsFilePath_ShouldReturnPath()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();

            // Act
            var filePath = _configManager.AppSettingsFilePath;

            // Assert
            Assert.That(filePath, Is.Not.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_WithoutConfiguration_ShouldFallbackGracefully()
        {
            // Arrange - No configuration setup
            TestStaticConfigurationHolder.Reset();

            // Act
            var result = _configManager.GetAppSetting("test.key");
            var filePath = _configManager.AppSettingsFilePath;

            // Assert
            Assert.That(result, Is.Null);
            Assert.That(filePath, Is.Not.Null); // Should still return a fallback path
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_WithExceptionInBridge_ShouldDisableAndReturnNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStaticBridged();
            
            // Force disable local config checks using reflection
            var field = typeof(ConfigurationManagerStaticBridged).GetField("localConfigChecksDisabled", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(configManager, true);

            // Act
            var result = configManager.GetAppSetting("test.key");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_MultipleInstances_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();
            
            var configManager1 = new ConfigurationManagerStaticBridged();
            var configManager2 = new ConfigurationManagerStaticBridged();

            // Act
            var result1 = configManager1.GetAppSetting("NewRelic.AppName");
            var result2 = configManager2.GetAppSetting("NewRelic.AppName");

            // Assert
            Assert.That(result1, Is.EqualTo("TestApplication"));
            Assert.That(result2, Is.EqualTo("TestApplication"));
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_RepeatedAccess_ShouldBeFast()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();

            // Warm up
            _configManager.GetAppSetting("NewRelic.AppName");

            // Act & Assert
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
            {
                _configManager.GetAppSetting("NewRelic.AppName");
            }
            stopwatch.Stop();

            // Should be fast (under 50ms for 100 calls)
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50), 
                "Repeated configuration access should be fast");
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_WithComplexConfiguration_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupComplexTestConfiguration();

            // Act
            var appName = _configManager.GetAppSetting("NewRelic.AppName");
            var connectionString = _configManager.GetAppSetting("ConnectionStrings:DefaultConnection");
            var nestedValue = _configManager.GetAppSetting("Section:Nested:Value");

            // Assert
            Assert.That(appName, Is.EqualTo("ComplexTestApp"));
            Assert.That(connectionString, Is.EqualTo("Server=localhost;Database=TestDb;"));
            Assert.That(nestedValue, Is.EqualTo("nested-config-value"));
        }

        [Test]
        public void ConfigurationManagerStaticBridged_NetStandard_ThreadSafety_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupTestConfiguration();
            
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

            // Act - Multiple concurrent access
            for (int i = 0; i < 10; i++)
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        var localConfigManager = new ConfigurationManagerStaticBridged();
                        for (int j = 0; j < 10; j++)
                        {
                            var result = localConfigManager.GetAppSetting("NewRelic.AppName");
                            Assert.That(result, Is.EqualTo("TestApplication"));
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                tasks.Add(task);
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.That(exceptions, Is.Empty, 
                $"Concurrent access caused exceptions: {string.Join(", ", exceptions.Select(e => e.Message))}");
        }

        private void SetupTestConfiguration()
        {
            var configPath = Path.Combine(_testDirectory, "appsettings.json");
            var configContent = @"{
  ""NewRelic.AppName"": ""TestApplication"",
  ""NewRelic.LicenseKey"": ""test-license-key-123"",
  ""NewRelic.AgentEnabled"": ""true""
}";
            File.WriteAllText(configPath, configContent);

            var builder = new ConfigurationBuilder()
                .SetBasePath(_testDirectory)
                .AddJsonFile("appsettings.json", optional: false);

            var configuration = builder.Build();
            TestStaticConfigurationHolder.Configuration = configuration;
        }

        private void SetupComplexTestConfiguration()
        {
            var configPath = Path.Combine(_testDirectory, "appsettings.json");
            var configContent = @"{
  ""NewRelic.AppName"": ""ComplexTestApp"",
  ""ConnectionStrings"": {
    ""DefaultConnection"": ""Server=localhost;Database=TestDb;""
  },
  ""Section"": {
    ""Nested"": {
      ""Value"": ""nested-config-value""
    }
  }
}";
            File.WriteAllText(configPath, configContent);

            var builder = new ConfigurationBuilder()
                .SetBasePath(_testDirectory)
                .AddJsonFile("appsettings.json", optional: false);

            var configuration = builder.Build();
            TestStaticConfigurationHolder.Configuration = configuration;
        }
#endif

        #endregion

        #region Cross-Platform Tests

        [Test]
        public void ConfigurationManagerStaticBridged_GetAppSetting_ShouldHandleNullAndEmptyValues()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _configManager.GetAppSetting(null));
            Assert.DoesNotThrow(() => _configManager.GetAppSetting(""));
            Assert.DoesNotThrow(() => _configManager.GetAppSetting("   "));

            Assert.That(_configManager.GetAppSetting(null), Is.Null);
            Assert.That(_configManager.GetAppSetting(""), Is.Null);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_AppSettingsFilePath_ShouldAlwaysReturnNonNullValue()
        {
            // Act
            var filePath = _configManager.AppSettingsFilePath;

            // Assert
            Assert.That(filePath, Is.Not.Null);
            Assert.That(filePath, Is.Not.Empty);
        }

        [Test]
        public void ConfigurationManagerStaticBridged_MultipleCallsToAppSettingsFilePath_ShouldReturnConsistentValue()
        {
            // Act
            var filePath1 = _configManager.AppSettingsFilePath;
            var filePath2 = _configManager.AppSettingsFilePath;
            var filePath3 = _configManager.AppSettingsFilePath;

            // Assert
            Assert.That(filePath1, Is.EqualTo(filePath2));
            Assert.That(filePath2, Is.EqualTo(filePath3));
        }

        [Test]
        public void ConfigurationManagerStaticBridged_GetAppSetting_ShouldBeThreadSafe()
        {
            // Arrange
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            var tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();

            // Act - Concurrent access from multiple threads
            for (int i = 0; i < 5; i++)
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 20; j++)
                        {
                            var result = _configManager.GetAppSetting($"test.key.{j}");
                            // Result might be null, but should not throw
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                });
                tasks.Add(task);
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert - Ultra optimized: Lazy evaluation, no string building unless failure occurs
            Assert.That(exceptions.Count, Is.EqualTo(0),
                () => {
                    var firstError = "none";
                    foreach (var ex in exceptions)
                    {
                        firstError = ex.Message;
                        break; // Get first exception only
                    }
                    return $"Thread safety test failed with {exceptions.Count} exception(s). First: {firstError}";
                });
        }

        #endregion
    }
}
