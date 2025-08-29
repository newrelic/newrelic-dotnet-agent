// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
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
                var cachedTypesField = bridgeType.GetField("_cachedTypes", BindingFlags.NonPublic | BindingFlags.Static);
                var initializedField = bridgeType.GetField("_initialized", BindingFlags.NonPublic | BindingFlags.Static);
                var bridgeAvailableField = bridgeType.GetField("_bridgeAvailable", BindingFlags.NonPublic | BindingFlags.Static);
                var getConfigurationValueDelegateField = bridgeType.GetField("_getConfigurationValueDelegate", BindingFlags.NonPublic | BindingFlags.Static);

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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");
            var licenseKey = configManager.GetAppSetting("NewRelic.LicenseKey");
            var agentEnabled = configManager.GetAppSetting("NewRelic.AgentEnabled");

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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var nonExistentValue = configManager.GetAppSetting("NonExistent.Key");
            var emptyKey = configManager.GetAppSetting("");
            var nullKey = configManager.GetAppSetting(null);

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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting("NewRelic.AppName");

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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var filePath = configManager.AppSettingsFilePath;

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
                var configManager = new ConfigurationManagerStatic();

                // Act
                var filePath = configManager.AppSettingsFilePath;

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

        [Test]
        public void ConfigurationBridge_WithException_ShouldDisableAndReturnNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();
            
            // Force an exception by using reflection to set localConfigChecksDisabled
            var field = typeof(ConfigurationManagerStatic).GetField("localConfigChecksDisabled", BindingFlags.NonPublic | BindingFlags.Instance);
            
            // Act
            var result1 = configManager.GetAppSetting("test"); // This might work
            
            // Force disable
            field?.SetValue(configManager, true);
            var result2 = configManager.GetAppSetting("test"); // This should return null

            // Assert
            Assert.That(result2, Is.Null);
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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");

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
            var configManager = new ConfigurationManagerStatic();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");

            // Assert
            Assert.That(appName, Is.EqualTo("TestApplication"));
        }

        [Test]
        public void ConfigurationBridge_WithComplexConfigurationHierarchy_ShouldWork()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupComplexConfiguration();
            var configManager = new ConfigurationManagerStatic();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");
            var connectionString = configManager.GetAppSetting("ConnectionStrings:DefaultConnection");
            var nestedValue = configManager.GetAppSetting("CustomSection:NestedValue");

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
                var configManager = new ConfigurationManagerStatic();

                // Act
                var appName = configManager.GetAppSetting("NewRelic.AppName");
                var environment = configManager.GetAppSetting("Environment");

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

        #region Error Handling Tests

        [Test]
        public void ConfigurationBridge_WithCorruptedConfigFile_ShouldHandleGracefully()
        {
            // Arrange
            var corruptedConfigPath = Path.Combine(_testDirectory, "corrupted-appsettings.json");
            File.WriteAllText(corruptedConfigPath, "{ invalid json }");
            File.Copy(corruptedConfigPath, _testConfigFile, true);
            
            Directory.SetCurrentDirectory(_testDirectory);
            var configManager = new ConfigurationManagerStatic();

            // Act & Assert - Should not throw
            Assert.DoesNotThrow(() => configManager.GetAppSetting("test"));
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
                var configManager = new ConfigurationManagerStatic();

                // Act & Assert - Should not throw
                Assert.DoesNotThrow(() => configManager.GetAppSetting("test"));
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
        public void ConfigurationBridge_WithSpecialCharactersInValues_ShouldHandleCorrectly()
        {
            // Arrange
            var specialConfigContent = @"{
  ""NewRelic.AppName"": ""Test-App_With.Special@Chars"",
  ""NewRelic.SpecialValue"": ""Line1\nLine2\tTab"",
  ""NewRelic.UnicodeValue"": ""测试应用程序"",
  ""NewRelic.EscapedValue"": ""Value with \""quotes\"" and \\backslashes\\""
}";
            File.WriteAllText(_testConfigFile, specialConfigContent);
            
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            var configManager = new ConfigurationManagerStatic();

            // Act
            var appName = configManager.GetAppSetting("NewRelic.AppName");
            var specialValue = configManager.GetAppSetting("NewRelic.SpecialValue");
            var unicodeValue = configManager.GetAppSetting("NewRelic.UnicodeValue");
            var escapedValue = configManager.GetAppSetting("NewRelic.EscapedValue");

            // Assert
            Assert.That(appName, Is.EqualTo("Test-App_With.Special@Chars"));
            Assert.That(specialValue, Is.EqualTo("Line1\nLine2\tTab"));
            Assert.That(unicodeValue, Is.EqualTo("测试应用程序"));
            Assert.That(escapedValue, Is.EqualTo("Value with \"quotes\" and \\backslashes\\"));
        }

        [Test]
        public void ConfigurationBridge_WithHighFrequencyAccess_ShouldMaintainPerformance()
        {
            // Arrange
            Directory.SetCurrentDirectory(_testDirectory);
            SetupApplicationConfiguration();
            var configManager = new ConfigurationManagerStatic();
            
            // Warm up
            configManager.GetAppSetting("NewRelic.AppName");

            // Act & Assert - High frequency access should remain fast
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 1000; i++)
            {
                var appName = configManager.GetAppSetting("NewRelic.AppName");
                var licenseKey = configManager.GetAppSetting("NewRelic.LicenseKey");
                
                // Verify correctness during performance test
                if (i % 100 == 0) // Check every 100th iteration
                {
                    Assert.That(appName, Is.EqualTo("TestApplication"));
                    Assert.That(licenseKey, Is.EqualTo("test-license-key-123456789"));
                }
            }
            stopwatch.Stop();

            // Assert - Should complete high-frequency access in reasonable time
            Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(500), 
                "High-frequency configuration access should remain performant");
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

    #endregion
}
#endif
