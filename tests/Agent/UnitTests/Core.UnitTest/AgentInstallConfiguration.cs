// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace NewRelic.Agent.Core
{
    [TestFixture]
    class AgentInstallConfigurationTests
    {
        [Test]
        public void AgentVersionTimeStampIsGreaterThanZero()
        {
            ClassicAssert.Greater(AgentInstallConfiguration.AgentVersionTimestamp, 0);
        }
    }
}
