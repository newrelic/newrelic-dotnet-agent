using System;
using System.Collections.Generic;
using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class CustomAttributesLegacyDisabled : IClassFixture<CustomAttributesWebApi>
    {
        private readonly CustomAttributesWebApi _fixture;

        public CustomAttributesLegacyDisabled(CustomAttributesWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath,
                        new[] { "configuration", "parameterGroups", "customParameters" }, "enabled", "false");
                },
                exerciseApplication: () =>
                {
                    _fixture.Get();
                    _fixture.Get404();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                }
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var unexpectedTransactionTraceAttributes = new List<String>
            {
                "key",
                "foo",
                "hey",
                "faz",
            };

            var transactionSample = _fixture.AgentLog.GetTransactionSamples().FirstOrDefault();
            var maybeDeprecationMessage = _fixture.AgentLog.TryGetLogLine(@".*NewRelic WARN: Deprecated configuration property 'parameterGroups.customParameters.ignore'.  Use 'attributes.exclude'.  See http://docs.newrelic.com for details.");

            Assert.NotNull(transactionSample);
            NrAssert.Multiple
            (
                () => Assertions.TransactionTraceDoesNotHaveAttributes(unexpectedTransactionTraceAttributes, TransactionTraceAttributeType.User, transactionSample),
                () => Assert.Null(maybeDeprecationMessage)
            );
        }
    }
}
