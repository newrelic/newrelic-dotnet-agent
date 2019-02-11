using NewRelic.Agent.IntegrationTestHelpers;
using PlatformTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace PlatformTests
{
	public class AzureWebApplicationSmokeTest : IClassFixture<AzureWebApplicationFixture>
	{
		private AzureWebApplicationFixture _fixture;

		private AgentLogString _agentLog;

		public AzureWebApplicationSmokeTest(AzureWebApplicationFixture fixture, ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;

			_fixture.Exercise = () =>
			{
				_fixture.WarmUp();
				var agentLogString = _fixture.GetAgentLog();
				_agentLog = new AgentLogString(agentLogString);
			};

			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var transactionSamples = _agentLog.GetTransactionSamples();
			Assert.NotEmpty(transactionSamples);
		}
	}
}
