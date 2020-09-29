// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CSP
{
    [NetCoreTest]
    public class AspNetCoreHSMEnabledWithCustomAttributesTests : IClassFixture<HSMAspNetCoreWebApiCustomAttributesFixture>
    {

        private readonly HSMAspNetCoreWebApiCustomAttributesFixture _fixture;

        public AspNetCoreHSMEnabledWithCustomAttributesTests(HSMAspNetCoreWebApiCustomAttributesFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level",
                        "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "highSecurity" },
                        "enabled", "true");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }

                );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedTransactionTraceAttributes = new List<string>
            {
                "key",
                "foo",
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionSample);
            Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample);
        }
    }
}
