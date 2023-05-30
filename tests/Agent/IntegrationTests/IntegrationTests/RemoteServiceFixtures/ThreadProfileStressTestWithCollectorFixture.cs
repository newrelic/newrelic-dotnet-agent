// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Diagnostics;
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
            var expectedWaitHandle = $"thread_profile_stress_begin_{Port}";
            Console.WriteLine("[{0}][PID: {1}] Setting signal to start the thread stress test.", ExecutableName, Process.GetCurrentProcess().Id);
            var remoteAppEvent = EventWaitHandle.OpenExisting(expectedWaitHandle);
            remoteAppEvent.Set();
        }
    }
}
