// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
    public class MvcAsyncFixture : RemoteApplicationFixture
    {
        public MvcAsyncFixture() : base(new RemoteWebApplication("MvcAsyncApplication", ApplicationType.Bounded))
        {
        }

        public void GetIoBoundNoSpecialAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/IoBoundNoSpecialAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetIoBoundConfigureAwaitFalseAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/IoBoundConfigureAwaitFalseAsync";
            GetStringAndAssertEqual(address, "Worked");
        }

        public void GetCpuBoundTasksAsync()
        {
            var address = $"http://{DestinationServerName}:{Port}/AsyncAwait/CpuBoundTasksAsync";
            GetStringAndAssertEqual(address, "Worked");
        }
    }
}
