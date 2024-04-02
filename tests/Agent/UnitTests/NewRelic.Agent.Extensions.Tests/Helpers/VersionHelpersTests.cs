// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.Extensions.Helpers;
using NUnit.Framework;

namespace Agent.Extensions.Tests.Helpers
{
    [TestFixture]
    public class VersionHelpersTests
    {
        [Test]
        [TestCase("ExampleAssembly, Version=1.2.3, Culture=neutral, PublicKeyToken=null", "1.2.3")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void GetVersion(string version, string expectedVersion)
        {
            // Act
            var actualVersion = VersionHelpers.GetLibraryVersion(version);

            // Assert
            Assert.That(actualVersion, Is.EqualTo(expectedVersion), "Did not get expected version");
        }
    }
}
