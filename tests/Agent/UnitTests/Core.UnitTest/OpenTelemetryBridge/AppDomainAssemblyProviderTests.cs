// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Linq;
using NewRelic.Agent.Core.OpenTelemetryBridge.Common;
using NUnit.Framework;

namespace NewRelic.Agent.Core.UnitTest.OpenTelemetryBridge
{
    [TestFixture]
    public class AppDomainAssemblyProviderTests
    {
        [Test]
        public void GetAssemblies_ReturnsLoadedAssemblies()
        {
            // Arrange
            var provider = new AppDomainAssemblyProvider();

            // Act
            var assemblies = provider.GetAssemblies().ToList();

            // Assert
            Assert.That(assemblies, Is.Not.Empty);
            Assert.That(assemblies.Any(a => a.GetName().Name == "mscorlib" || a.GetName().Name == "System.Private.CoreLib"), Is.True);
        }

        [Test]
        public void GetAssemblies_ReturnsCurrentTestAssembly()
        {
            // Arrange
            var provider = new AppDomainAssemblyProvider();

            // Act
            var assemblies = provider.GetAssemblies().ToList();

            // Assert
            Assert.That(assemblies.Any(a => a.GetName().Name.Contains("Core.UnitTest")), Is.True);
        }
    }
}
