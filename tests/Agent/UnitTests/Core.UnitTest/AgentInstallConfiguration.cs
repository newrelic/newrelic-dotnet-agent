// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core
{
    [TestFixture]
    class AgentInstallConfigurationTests
    {
        [Test]
        public void AgentVersionTimeStampIsGreaterThanZero()
        {
            Assert.That(AgentInstallConfiguration.AgentVersionTimestamp, Is.GreaterThan(0));
        }
    }
}
