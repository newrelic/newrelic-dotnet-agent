// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AzureFunction
{
    [NetCoreTest]
    public class AzureFunctionInstrumentationDisabledTestsCoreLatest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest>
    {
        public AzureFunctionInstrumentationDisabledTestsCoreLatest(AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
