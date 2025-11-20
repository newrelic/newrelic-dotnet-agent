// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace CompositeTests
{
    [TestFixture]
    public class EnvironmentTests
    {
        private NewRelic.Agent.Core.SharedInterfaces.Environment _env;

        [SetUp]
        public void Setup()
        {
            System.Environment.SetEnvironmentVariable("NEWRELIC_HOME", null);
            System.Environment.SetEnvironmentVariable("NEW_RELIC_HOME", null);
            NewRelic.Agent.Core.SharedInterfaces.Environment.ResetCache();

            _env = new NewRelic.Agent.Core.SharedInterfaces.Environment();
        }

        [TearDown]
        public void TearDown()
        {
            System.Environment.SetEnvironmentVariable("NEWRELIC_HOME", null);
            System.Environment.SetEnvironmentVariable("NEW_RELIC_HOME", null);
            NewRelic.Agent.Core.SharedInterfaces.Environment.ResetCache();
        }

        [Test]
        public void GetEnvironmentVariable_Supports_LegacyNaming()
        {
            System.Environment.SetEnvironmentVariable("NEWRELIC_HOME", "legacy");

            var result = _env.GetEnvironmentVariableFromList("NEW_RELIC_HOME", "NEWRELIC_HOME");

            Assert.That(result, Is.EqualTo("legacy"));
        }

        [Test]
        public void GetEnvironmentVariable_Prefers_ModernNaming_IfBothAreSpecified()
        {
            System.Environment.SetEnvironmentVariable("NEWRELIC_HOME", "legacy");
            System.Environment.SetEnvironmentVariable("NEW_RELIC_HOME", "modern");

            var result = _env.GetEnvironmentVariableFromList("NEW_RELIC_HOME", "NEWRELIC_HOME");

            Assert.That(result, Is.EqualTo("modern"));
        }
    }
}
