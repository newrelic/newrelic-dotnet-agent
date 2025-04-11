// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.AgentFeatures
{
    public class RemotingSerialization : NewRelicIntegrationTest<RemoteServiceFixtures.OwinRemotingFixture>
    {
        private readonly RemoteServiceFixtures.OwinRemotingFixture _fixture;

        string _tcpResponse;
        string _httpResponse;

        public RemotingSerialization(RemoteServiceFixtures.OwinRemotingFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.AddActions(

                exerciseApplication: () =>
                {
                    _tcpResponse = _fixture.GetObjectTcp();
                    _httpResponse = _fixture.GetObjectHttp();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.True(_tcpResponse == "\"No exception\"");
            Assert.True(_httpResponse == "\"No exception\"");
        }
    }
}
