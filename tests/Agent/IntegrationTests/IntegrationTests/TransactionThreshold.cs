using System.Linq;
using NewRelic.Agent.IntegrationTestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
    public class TransactionThreshold : IClassFixture<RemoteServiceFixtures.BasicWebApi>
    {
        private readonly RemoteServiceFixtures.BasicWebApi _fixture;

        public TransactionThreshold(RemoteServiceFixtures.BasicWebApi fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _fixture.TestLogger = output;
            _fixture.Actions
            (
                setupConfiguration: () =>
                {
                    var configPath = fixture.DestinationNewRelicConfigFilePath;

                    var configModifier = new NewRelicConfigModifier(configPath);

                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "log" }, "level", "debug");
                    CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] { "configuration", "transactionTracer" }, "transactionThreshold", "100000");
                },
                exerciseApplication: () => _fixture.Get()
            );
            _fixture.Initialize();
        }

        [Fact]
        public void Test()
        {
            Assert.False(_fixture.AgentLog.GetTransactionSamples().Any(), "Transaction trace found when none were expected.");
        }
    }
}
