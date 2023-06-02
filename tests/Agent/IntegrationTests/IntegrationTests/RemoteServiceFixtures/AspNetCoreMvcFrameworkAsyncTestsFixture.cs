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
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomMiddlewareIoBoundNoSpecialAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://localhost:{Port}/AsyncAwaitTest/CpuBoundTasksAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskRunBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskRunBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskFactoryStartNewBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/TaskFactoryStartNewBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualNewThreadStartBlocked()
        {
            var address = $"http://localhost:{Port}/ManualAsync/NewThreadStartBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }

    }
}
