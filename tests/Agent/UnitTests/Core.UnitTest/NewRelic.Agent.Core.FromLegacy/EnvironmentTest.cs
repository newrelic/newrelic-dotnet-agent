// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilities;
using NewRelic.Agent.Core.SharedInterfaces;
using NUnit.Framework;
using Telerik.JustMock;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    public class EnvironmentTest
    {
        [Test]
        public static void TestTotalMemory()
        {
            var configurationService = Mock.Create<IConfigurationService>();
            var systemInfo = Mock.Create<ISystemInfo>();
            var processStatic = Mock.Create<IProcessStatic>();

            Mock.Arrange(() => systemInfo.GetTotalPhysicalMemoryBytes()).Returns(16000);
            using (new ConfigurationAutoResponder(configurationService.Configuration))
            {
                var env = new Environment(systemInfo, processStatic, configurationService);
                Assert.That(env.TotalPhysicalMemory, Is.GreaterThan(0));
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly",
             MessageId = "WWW"), Test]
        public static void TestGetAppPathWithWWWRoot()
        {
            Assert.That(
                Environment.TryGetAppPath(() =>
                    "c:" + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "myapp" +
                    Path.DirectorySeparatorChar + "WwwRoot"), Is.EqualTo("myapp"));
        }

        [Test]
        public static void TestGetAppPathTrailingSlash()
        {
            Assert.That(
                Environment.TryGetAppPath(() =>
                    "c:" + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "Dude" +
                    Path.DirectorySeparatorChar), Is.EqualTo("Dude"));
        }

        [Test]
        public static void TestEnvironmentSerialization()
        {
            var configurationService = Mock.Create<IConfigurationService>();
            var systemInfo = Mock.Create<ISystemInfo>();
            var processStatic = Mock.Create<IProcessStatic>();

            Mock.Arrange(() => systemInfo.GetTotalPhysicalMemoryBytes()).Returns(16000);
            Mock.Arrange(() => configurationService.Configuration.ApplicationNames)
                .Returns(new List<string> { "TestApp" });
            Mock.Arrange(() => configurationService.Configuration.ApplicationNamesSource).Returns("TestSource");
            Mock.Arrange(() => configurationService.Configuration.NewRelicConfigFilePath).Returns("configPath");
            Mock.Arrange(() => configurationService.Configuration.AppSettingsConfigFilePath).Returns("appSettingsPath");

            using (new ConfigurationAutoResponder(configurationService.Configuration))
            {
                var env = new Environment(systemInfo, processStatic, configurationService);
                var json = JsonConvert.SerializeObject(env);

                Assert.Multiple(() =>
                {
                    Assert.That(json, Is.Not.Null);
                    Assert.That(json, Does.Contain("Total Physical System Memory"));
                    Assert.That(json, Does.Contain("16000"));
                    Assert.That(json, Does.Contain("Initial Application Names"));
                    Assert.That(json, Does.Contain("TestApp"));
                    Assert.That(json, Does.Contain("Initial Application Names Source"));
                    Assert.That(json, Does.Contain("TestSource"));
                    Assert.That(json, Does.Contain("Initial NewRelic Config"));
                    Assert.That(json, Does.Contain("configPath"));
                    Assert.That(json, Does.Contain("Application Config"));
                    Assert.That(json, Does.Contain("appSettingsPath"));
                });
            }
        }

        [Test]
        public static void TestEnvironmentSerializationWithNullValues()
        {
            var configurationService = Mock.Create<IConfigurationService>();
            var systemInfo = Mock.Create<ISystemInfo>();
            var processStatic = Mock.Create<IProcessStatic>();

            Mock.Arrange(() => systemInfo.GetTotalPhysicalMemoryBytes()).Returns((ulong?)null);
            Mock.Arrange(() => configurationService.Configuration.ApplicationNames).Returns(new List<string>());
            Mock.Arrange(() => configurationService.Configuration.ApplicationNamesSource).Returns((string)null);
            Mock.Arrange(() => configurationService.Configuration.NewRelicConfigFilePath).Returns((string)null);
            Mock.Arrange(() => configurationService.Configuration.AppSettingsConfigFilePath).Returns((string)null);

            using (new ConfigurationAutoResponder(configurationService.Configuration))
            {
                var env = new Environment(systemInfo, processStatic, configurationService);
                var json = JsonConvert.SerializeObject(env);

                Assert.Multiple(() =>
                {
                    Assert.That(json, Is.Not.Null);
                    Assert.That(json, Does.Contain("Total Physical System Memory"));
                    Assert.That(json, Does.Contain("Initial NewRelic Config"));

                    // won't be present because we check for a non-zero list of application names
                    Assert.That(json, Does.Not.Contain("Initial Application Names"));
                    Assert.That(json, Does.Not.Contain("Initial Application Names Source"));

                    // won't be present because we check for null 
                    Assert.That(json, Does.Not.Contain("Application Config"));
                });
            }
        }
    }
}
