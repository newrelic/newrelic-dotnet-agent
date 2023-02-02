// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using MultiFunctionApplicationHelpers;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.DataTransmission
{
    public abstract class ConnectSetApplicationNameTestsBase<TFixture> : NewRelicIntegrationTest<TFixture>
        where TFixture : ConsoleDynamicMethodFixture
    {
        private readonly TFixture _fixture;

        public ConnectSetApplicationNameTestsBase(TFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.SetTimeout(TimeSpan.FromMinutes(1));
            _fixture.TestLogger = output;

            _fixture.AddCommand($"ApiCalls TestSetApplicationName NewIntegrationTestName");

            // Needed to ensure that the scheduled reconnect, with a 15 second delay, can happen
            _fixture.AddCommand($"RootCommands DelaySeconds 30");

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                    configModifier
                    .SetLogLevel("debug");

                    // if sendDataOnExit is true, this test will fail due to it blocking on connect.
                    configModifier.SetSendDataOnExit(false);
                },
                exerciseApplication: () =>
                {
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SetApplicationnameAPICalledDuringCollectMethodLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AttemptReconnectLogLineRegex, TimeSpan.FromMinutes(1));
                }
            );

            _fixture.Initialize();
        }

        [Fact]
        public void Tests()
        {
            var connectResponseDatas = _fixture.AgentLog.GetConnectResponseDatas() .ToList();
            Assert.Equal(2, connectResponseDatas.Count);

            var reconnectLine = _fixture.AgentLog.TryGetLogLines(AgentLogBase.AttemptReconnectLogLineRegex);
            Assert.True(reconnectLine.Any());
        }
    }

    [NetFrameworkTest]
    public class ConnectSetApplicationNameFWLatestTests : ConnectSetApplicationNameTestsBase<ConsoleDynamicMethodFixtureFWLatest>
    {
        public ConnectSetApplicationNameFWLatestTests(ConsoleDynamicMethodFixtureFWLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetFrameworkTest]
    public class ConnectSetApplicationNameFW462Tests : ConnectSetApplicationNameTestsBase<ConsoleDynamicMethodFixtureFW462>
    {
        public ConnectSetApplicationNameFW462Tests(ConsoleDynamicMethodFixtureFW462 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ConnectSetApplicationNameCoreLatestTests : ConnectSetApplicationNameTestsBase<ConsoleDynamicMethodFixtureCoreLatest>
    {
        public ConnectSetApplicationNameCoreLatestTests(ConsoleDynamicMethodFixtureCoreLatest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }

    [NetCoreTest]
    public class ConnectSetApplicationNameCore60Tests : ConnectSetApplicationNameTestsBase<ConsoleDynamicMethodFixtureCore60>
    {
        public ConnectSetApplicationNameCore60Tests(ConsoleDynamicMethodFixtureCore60 fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
