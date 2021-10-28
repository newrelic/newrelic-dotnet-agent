// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcAsyncTestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMvcAsyncApplication";
        private const string ExecutableName = @"AspNetCoreMvcAsyncApplication.exe";

        public AspNetCoreMvcAsyncTestsFixture() : base(new RemoteService(ApplicationDirectoryName, ExecutableName, "netcoreapp3.1", ApplicationType.Bounded, true, true, true))
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
