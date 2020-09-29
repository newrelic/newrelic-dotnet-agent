// Copyright 2020 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0


using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests.CustomAttributes
{
    [NetFrameworkTest]
    public class CustomAttributesLegacyIgnored : IClassFixture<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesLegacyIgnored(RemoteServiceFixtures.CustomAttributesWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlNodeInNewRelicConfig(configPath,
                        new[] { "configuration", "parameterGroups", "customParameters" }, "ignore", "key");
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
            var expectedTransactionTraceAttributes = new Dictionary<string, string>
            {
                { "foo", "bar" }
            };
            var unexpectedTransactionTraceAttributes = new List<string>
            {
                "key"
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            Assert.NotNull(transactionSample);
            var maybeDeprecationMessage = _fixture.AgentLog.TryGetLogLine(AgentLogBase.WarnLogLinePrefixRegex + @"Deprecated configuration property 'parameterGroups.customParameters.ignore'.  Use 'attributes.exclude'.  See http://docs.newrelic.com for details.");

            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceHasAttributes(expectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assert.NotNull(maybeDeprecationMessage)
            );
        }
    }
}
