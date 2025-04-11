// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.IntegrationTests.Models;
using System.Collections.Generic;
using System.Linq;
using Xunit;


namespace NewRelic.Agent.IntegrationTests.DataTransmission
{
    public class DataTransmissionDefaults : NewRelicIntegrationTest<MvcWithCollectorFixture>
    {
        private readonly MvcWithCollectorFixture _fixture;

        private IEnumerable<CollectedRequest> _collectedRequests = null;

        public DataTransmissionDefaults(MvcWithCollectorFixture fixture, ITestOutputHelper output) : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AddActions(
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.AgentConnectedLogLineRegex, TimeSpan.FromMinutes(1));
                    _collectedRequests = _fixture.GetCollectedRequests();
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.NotNull(_collectedRequests);
            var request = _collectedRequests.FirstOrDefault(x => x.Querystring.FirstOrDefault(y => y.Key == "method").Value == "connect");
            Assert.NotNull(request);
            Assert.True(request.Method == "POST");
            Assert.True(request.ContentEncoding.First() == "deflate");
            var decompressedBody = Decompressor.DeflateDecompress(request.RequestBody);
            Assert.NotEmpty(decompressedBody);
        }
    }
}
