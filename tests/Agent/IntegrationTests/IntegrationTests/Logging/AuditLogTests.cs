// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.Logging.AuditLog
{
    [NetCoreTest]
    public class AuditLogTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public AuditLogTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture  fixture, ITestOutputHelper output) :
            base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;

            _fixture.AuditLogExpected = true;

            _fixture.AddActions
            (
                setupConfiguration: () =>
                {
                    var configModifier = new NewRelicConfigModifier(fixture.DestinationNewRelicConfigFilePath);

                    configModifier.EnableAuditLog(true);
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();

                    _fixture.AgentLog.WaitForLogLine(AgentLogBase.ShutdownLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );

            _fixture.Initialize();

        }

        [Fact]
        public void AuditLogExistsAndHasSentAndReceivedData()
        {
            Assert.True(_fixture.AuditLog.Found);

            var dataSentLogLines =  _fixture.AuditLog.TryGetLogLines(AuditLogFile.AuditDataSentLogLineRegex).ToList();
            var dataReceivedLogLines = _fixture.AuditLog.TryGetLogLines(AuditLogFile.AuditDataReceivedLogLineRegex).ToList();

            Assert.Multiple(() =>
            {
                Assert.True(dataSentLogLines.Count == 2 * dataReceivedLogLines.Count); // audit log always contains 2 "sent" lines for every "received" line
            });
        }
    }
}
