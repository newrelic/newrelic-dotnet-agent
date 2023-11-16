// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class AspNetCoreMvcAsyncTestsFixture : RemoteApplicationFixture
    {
        private const string ApplicationDirectoryName = @"AspNetCoreMvcAsyncApplication";
        private const string ExecutableName = @"AspNetCoreMvcAsyncApplication.exe";

        public AspNetCoreMvcAsyncTestsFixture() :
            base(new RemoteService(
                ApplicationDirectoryName,
                ExecutableName,
                "net8.0",
                ApplicationType.Bounded,
                true,
                true,
                true))
        {
        }

        public void GetIoBoundNoSpecialAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwaitTest/IoBoundNoSpecialAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCustomMiddlewareIoBoundNoSpecialAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwaitTest/CustomMiddlewareIoBoundNoSpecialAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwaitTest/IoBoundConfigureAwaitFalseAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwaitTest/CpuBoundTasksAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskRunBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/TaskRunBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualTaskFactoryStartNewBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/TaskFactoryStartNewBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetManualNewThreadStartBlocked()
        {
            var address = $"http://{DestinationServerName}:{Port}/ManualAsync/NewThreadStartBlocked";
            GetStringAndAssertEqual(address, "Worked");
        }
    }
}
