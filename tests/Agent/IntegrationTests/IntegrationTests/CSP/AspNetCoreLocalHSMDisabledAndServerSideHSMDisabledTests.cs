// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.Tests.TestSerializationHelpers.Models;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetCoreTest]
    public class AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTests : NewRelicIntegrationTest<RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture>
    {
        private const string QueryStringParameterValue = @"my thing";


        private readonly RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture _fixture;

        public AspNetCoreLocalHSMDisabledAndServerSideHSMDisabledTests(RemoteServiceFixtures.AspNetCoreMvcBasicRequestsFixture fixture, ITestOutputHelper output)
            : base(fixture)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();
                    configModifier.AddAttributesInclude("request.parameters.*");

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "service" }, "ssl", "false");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "recordSql", "raw");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" }, "enabled", "false");
                },
                exerciseApplication: () => _fixture.GetWithData(QueryStringParameterValue)
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedAttributes = new Dictionary<string, string>
            {
                { "request.parameters.data", QueryStringParameterValue },
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assertions.TransactionTraceHasAttributes(expectedAttributes, TransactionTraceAttributeType.Agent, transactionSample);
        }
    }
}
