// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NUnit.Framework;

namespace NewRelic.Agent.Core.Utils
{
    [TestFixture]
    public class RuntimeEnvironmentInfoTests
    {
        [Test]
        public void RuntimeEnvironmentInfo_OperatingSystem_ReportsWindows()
        {
            Assert.AreEqual("Windows", RuntimeEnvironmentInfo.OperatingSystem);
        }

        [Test]
        public void RuntimeEnvironmentInfo_OperatingSystemVersion_ReportsNotNull()
        {
            Assert.NotNull(RuntimeEnvironmentInfo.OperatingSystemVersion);
        }
    }
}
