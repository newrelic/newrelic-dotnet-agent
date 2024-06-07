// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.IO;
using NewRelic.Agent.Configuration;
using NewRelic.Agent.Core.Fixtures;
using NewRelic.Agent.Core.Utilities;
using NewRelic.SystemInterfaces;
using NUnit.Framework;
using Telerik.JustMock;

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

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1709:IdentifiersShouldBeCasedCorrectly", MessageId = "WWW"), Test]
        public static void TestGetAppPathWithWWWRoot()
        {
            Assert.That(Environment.TryGetAppPath(() => "c:" + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "myapp" + Path.DirectorySeparatorChar + "WwwRoot"), Is.EqualTo("myapp"));
        }

        [Test]
        public static void TestGetAppPathTrailingSlash()
        {
            Assert.That(Environment.TryGetAppPath(() => "c:" + Path.DirectorySeparatorChar + "test" + Path.DirectorySeparatorChar + "Dude" + Path.DirectorySeparatorChar), Is.EqualTo("Dude"));
        }
    }
}
