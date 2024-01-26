// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class RuntimeEnvironmentInfoTests
    {
        [Test]
        [Platform("Win")] // only run this test on a windows platform
        public void RuntimeEnvironmentInfo_OperatingSystem_ReportsWindows_OnWindows()
        {
            Assert.That(RuntimeEnvironmentInfo.OperatingSystem, Is.EqualTo("Windows"));
        }

        [Test]
        public void RuntimeEnvironmentInfo_OperatingSystemVersion_ReportsNotNull()
        {
            Assert.That(RuntimeEnvironmentInfo.OperatingSystemVersion, Is.Not.Null);
        }
    }
}
