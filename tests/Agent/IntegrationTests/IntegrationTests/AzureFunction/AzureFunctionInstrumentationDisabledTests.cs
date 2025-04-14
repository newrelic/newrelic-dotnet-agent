// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;

namespace NewRelic.Agent.IntegrationTests.AzureFunction;

public class AzureFunctionInstrumentationDisabledTestsCoreLatest : AzureFunctionHttpTriggerTestsBase<AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest>
{
    public AzureFunctionInstrumentationDisabledTestsCoreLatest(AzureFunctionApplicationFixtureInstrumentationDisabledCoreLatest fixture, ITestOutputHelper output)
        : base(fixture, output, AzureFunctionHttpTriggerTestMode.AspNetCorePipeline) // test mode doesn't really matter here
    {
    }
}
