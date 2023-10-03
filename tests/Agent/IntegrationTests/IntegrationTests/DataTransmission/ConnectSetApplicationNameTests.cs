// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
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
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.SetApplicationnameAPICalledDuringConnectMethodLogLineRegex, TimeSpan.FromMinutes(1));
                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.AttemptReconnectLogLineRegex, TimeSpan.FromMinutes(1));
                    // There should be two connected log lines, one for the initial connect and the other after the reconnect
                    _fixture.AgentLog.WaitForLogLines(AgentLogBase.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1), 2);
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
    public class ConnectSetApplicationNameCoreOldestTests : ConnectSetApplicationNameTestsBase<ConsoleDynamicMethodFixtureCoreOldest>
    {
        public ConnectSetApplicationNameCoreOldestTests(ConsoleDynamicMethodFixtureCoreOldest fixture, ITestOutputHelper output)
            : base(fixture, output)
        {
        }
    }
}
