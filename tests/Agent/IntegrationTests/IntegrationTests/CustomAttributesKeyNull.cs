using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class CustomAttributesKeyNull : IClassFixture<RemoteServiceFixtures.CustomAttributesWebApi>
    {
        [NotNull]
        private readonly RemoteServiceFixtures.CustomAttributesWebApi _fixture;

        public CustomAttributesKeyNull([NotNull] RemoteServiceFixtures.CustomAttributesWebApi fixture, [NotNull] ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions(
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;
                    var configModifier = new NewRelicConfigModifier(configPath);
                    configModifier.ForceTransactionTraces();

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                },
                exerciseApplication: () =>
                {
                    _fixture.GetKeyNull();
                    _fixture.AgentLog.WaitForLogLine(AgentLogFile.TransactionSampleLogLineRegex, TimeSpan.FromMinutes(2));
                });
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            var expectedTransactionName = @"WebTransaction/WebAPI/My/CustomAttributesKeyNull";

            var unexpectedTransactionEventAttributes = new List<String>
            {
                "keywithnullvalue"
            };

            var transactionEvent = _fixture.AgentLog.TryGetTransactionEvent(expectedTransactionName);

            NrAssert.Multiple
            (
                () => Assertions.TransactionEventDoesNotHaveAttributes(unexpectedTransactionEventAttributes, TransactionEventAttributeType.User, transactionEvent)
            );
        }
    }
}
