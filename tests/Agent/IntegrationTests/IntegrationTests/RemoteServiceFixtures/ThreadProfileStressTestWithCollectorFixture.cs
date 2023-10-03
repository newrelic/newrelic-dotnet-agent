// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class ThreadProfileStressTestWithCollectorFixture : MockNewRelicFixture
    {
        private const string ApplicationDirectoryName = @"ThreadProfileStressTest";
        private const string ExecutableName = @"ThreadProfileStressTest.exe";
        public ThreadProfileStressTestWithCollectorFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, ApplicationType.Bounded))
        {
        }

        public void StartThreadStressScenario()
        {
            Debug.Assert(RuntimeInformation.IsOSPlatform(OSPlatform.Windows));
            var expectedWaitHandle = $"thread_profile_stress_begin_{Port}";
            var remoteAppEvent = EventWaitHandle.OpenExisting(expectedWaitHandle);
            remoteAppEvent.Set();
        }
    }
}
