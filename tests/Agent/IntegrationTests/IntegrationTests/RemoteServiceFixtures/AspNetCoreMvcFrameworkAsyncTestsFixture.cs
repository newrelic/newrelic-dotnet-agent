// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcFrameworkAsyncTestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMvcFrameworkAsyncApplication";
        private const string ExecutableName = @"AspNetCoreMvcFrameworkAsyncApplication.exe";
        private const string TargetFramework = "net462";


        public AspNetCoreMvcFrameworkAsyncTestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded, true, false, true))
        {
        }

        public void GetIoBoundNoSpecialAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/IoBoundNoSpecialAsync";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomMiddlewareIoBoundNoSpecialAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/CpuBoundTasksAsync";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskRunBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskRunBlocked";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskFactoryStartNewBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskFactoryStartNewBlocked";
            DownloadStringAndAssertEqual(address, "Worked");
        }

        public void GetManualNewThreadStartBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/NewThreadStartBlocked";
            DownloadStringAndAssertEqual(address, "Worked");
        }

    }
}
