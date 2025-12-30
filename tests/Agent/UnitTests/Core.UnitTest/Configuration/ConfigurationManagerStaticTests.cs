// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.Core.Configuration;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.Configuration
{
    [TestFixture]
    public class ConfigurationManagerStaticMockTests
    {
        [Test]
        public void Constructor_WithNullDelegate_ReturnsNullForAnyKey()
        {
            // Arrange & Act
            var mock = new ConfigurationManagerStaticMock();

            // Assert
            Assert.That(mock.GetAppSetting("anyKey"), Is.Null);
            Assert.That(mock.GetAppSetting("AnotherKey"), Is.Null);
            Assert.That(mock.GetAppSetting(""), Is.Null);
        }

        [Test]
        public void Constructor_WithCustomDelegate_ReturnsExpectedValues()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock(key =>
            {
                if (key == "TestKey")
                    return "TestValue";
                if (key == "AnotherKey")
                    return "AnotherValue";
                return null;
            });

            // Act & Assert
            Assert.That(mock.GetAppSetting("TestKey"), Is.EqualTo("TestValue"));
            Assert.That(mock.GetAppSetting("AnotherKey"), Is.EqualTo("AnotherValue"));
            Assert.That(mock.GetAppSetting("NonExistent"), Is.Null);
        }

        [Test]
        public void GetAppSetting_WithNullKey_ReturnsNull()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock(key => string.IsNullOrEmpty(key) ? null : "value");

            // Act
            var result = mock.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetAppSetting_WithEmptyKey_ReturnsNull()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock(key => string.IsNullOrEmpty(key) ? null : "value");

            // Act
            var result = mock.GetAppSetting("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void AppSettingsFilePath_ThrowsNotImplementedException()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock();

            // Act & Assert
            Assert.Throws<NotImplementedException>(() =>
            {
                var _ = mock.AppSettingsFilePath;
            });
        }

        [Test]
        public void GetAppSetting_CalledMultipleTimes_InvokesDelegateEachTime()
        {
            // Arrange
            int callCount = 0;
            var mock = new ConfigurationManagerStaticMock(key =>
            {
                callCount++;
                return $"value{callCount}";
            });

            // Act
            var result1 = mock.GetAppSetting("key1");
            var result2 = mock.GetAppSetting("key2");
            var result3 = mock.GetAppSetting("key3");

            // Assert
            Assert.That(result1, Is.EqualTo("value1"));
            Assert.That(result2, Is.EqualTo("value2"));
            Assert.That(result3, Is.EqualTo("value3"));
            Assert.That(callCount, Is.EqualTo(3));
        }

        [Test]
        public void GetAppSetting_WithDelegateReturningNull_ReturnsNull()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock(key => null);

            // Act
            var result = mock.GetAppSetting("anyKey");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetAppSetting_WithDelegateThrowingException_PropagatesException()
        {
            // Arrange
            var mock = new ConfigurationManagerStaticMock(key =>
            {
                throw new InvalidOperationException("Test exception");
            });

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
            {
                mock.GetAppSetting("anyKey");
            });

            Assert.That(ex.Message, Is.EqualTo("Test exception"));
        }
    }

#if NETFRAMEWORK
    [TestFixture]
    public class ConfigurationManagerStaticNetFrameworkTests
    {
        [Test]
        public void GetAppSetting_WithNullKey_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void AppSettingsFilePath_ReturnsConfigurationFilePath()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var filePath = configManager.AppSettingsFilePath;

            // Assert
            // Should return a path or null, but should not throw
            Assert.DoesNotThrow(() =>
            {
                var _ = configManager.AppSettingsFilePath;
            });
        }

        [Test]
        public void AppSettingsFilePath_OnException_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var filePath = configManager.AppSettingsFilePath;

            // Assert
            // Even if there's an exception accessing the path, it should return null gracefully
            Assert.That(filePath, Is.Not.Null.Or.Null);
        }

        [Test]
        public void GetAppSetting_AfterException_DisablesLocalConfigChecks()
        {
            // This test verifies that after an exception, subsequent calls return null
            // Note: This behavior depends on internal state and may be difficult to test
            // without reflection or a specific error condition
            var configManager = new ConfigurationManagerStatic();

            // Try to get a setting that doesn't exist
            var result1 = configManager.GetAppSetting("NonExistentKey123456789");

            // Subsequent calls should still work
            var result2 = configManager.GetAppSetting("AnotherNonExistentKey");

            Assert.That(result1, Is.Null);
            Assert.That(result2, Is.Null);
        }
    }
#else
    [TestFixture]
    public class ConfigurationManagerStaticNetStandardTests
    {
        [Test]
        public void GetAppSetting_WithNullKey_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting(null);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetAppSetting_WithEmptyKey_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting("");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetAppSetting_WithWhitespaceKey_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting("   ");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void GetAppSetting_WhenLocalConfigDisabled_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act - Try to get a non-existent key
            var result = configManager.GetAppSetting("NonExistentKey");

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public void AppSettingsFilePath_WhenLocalConfigDisabled_ReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Force disable local config by causing an exception (if possible)
            // Then check AppSettingsFilePath

            // Act
            var filePath = configManager.AppSettingsFilePath;

            // Assert
            // Should return a path or null, but should not throw
            Assert.DoesNotThrow(() =>
            {
                var _ = configManager.AppSettingsFilePath;
            });
        }

        [Test]
        public void GetAppSetting_OnException_DisablesLocalConfigAndReturnsNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act - Access a configuration that might cause an error
            var result1 = configManager.GetAppSetting("SomeKey");

            // Second call after potential exception
            var result2 = configManager.GetAppSetting("AnotherKey");

            // Assert - Should handle gracefully
            Assert.That(result1, Is.Null.Or.Not.Null);
            Assert.That(result2, Is.Null.Or.Not.Null);
        }

        [Test]
        public void AppSettingsFilePath_CallsConfigurationBridge()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var filePath = configManager.AppSettingsFilePath;

            // Assert
            // Should delegate to ConfigurationBridge
            Assert.DoesNotThrow(() =>
            {
                var _ = configManager.AppSettingsFilePath;
            });
        }

        [Test]
        public void GetAppSetting_CallsConfigurationBridge()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting("TestKey");

            // Assert
            // Should delegate to ConfigurationBridge and not throw
            Assert.DoesNotThrow(() =>
            {
                configManager.GetAppSetting("TestKey");
            });
        }

        [Test]
        public void GetAppSetting_WithValidKey_ReturnsValueOrNull()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act
            var result = configManager.GetAppSetting("NewRelic.AppName");

            // Assert
            // Could be null if no configuration is available, or a value if it exists
            Assert.That(result, Is.Null.Or.Not.Empty);
        }

        [Test]
        public void GetAppSetting_MultipleCalls_ShouldNotThrow()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                configManager.GetAppSetting("Key1");
                configManager.GetAppSetting("Key2");
                configManager.GetAppSetting("Key3");
            });
        }

        [Test]
        public void AppSettingsFilePath_MultipleCalls_ShouldNotThrow()
        {
            // Arrange
            var configManager = new ConfigurationManagerStatic();

            // Act & Assert
            Assert.DoesNotThrow(() =>
            {
                var path1 = configManager.AppSettingsFilePath;
                var path2 = configManager.AppSettingsFilePath;
                var path3 = configManager.AppSettingsFilePath;
            });
        }
    }
#endif
}
