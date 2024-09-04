// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AzureFunction
{
    public abstract class AzureFunctionTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : AzureFunctionApplicationFixture
    {
        private readonly TFixture _fixture;

        protected AzureFunctionTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath)
                        .SetLogLevel("finest");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get("api/function1");
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.TransactionTransformCompletedLogLineRegex, TimeSpan.FromMinutes(2), 1);
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {

            Assert.True(true);
        }
    }

    [NetCoreTest]
    public class AzureFunctionTestsCoreLatest : AzureFunctionTestsBase<AzureFunctionApplicationFixture_Function1_CoreLatest>
    {
        public AzureFunctionTestsCoreLatest(AzureFunctionApplicationFixture_Function1_CoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
