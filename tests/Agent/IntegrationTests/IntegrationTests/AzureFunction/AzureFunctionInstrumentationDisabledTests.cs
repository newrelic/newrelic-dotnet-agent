// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AzureFunction;

[NetCoreTest]
public class AzureFunctionInstrumentationDisabledTestsCoreOldest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureInstrumentationDisabledCoreOldest>
{
    public AzureFunctionInstrumentationDisabledTestsCoreOldest(AzureFunctionApplicationFixtureInstrumentationDisabledCoreOldest fixture, ITestOutputHelper output)
        : base(fixture, output, AzureFunctionHttpTriggerTestMode.AspNetCorePipeline) // test mode doesn't really matter here
    {
    }
}
