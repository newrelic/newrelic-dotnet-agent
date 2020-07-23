using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Agent.IntegrationTests.RemoteServiceFixtures;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class AspNetCoreHSMEnabledWithCustomAttributesTests : IClassFixture<HSMAspNetCoreWebApiCustomAttributesFixture>
	{
		[NotNull]
		private readonly HSMAspNetCoreWebApiCustomAttributesFixture _fixture;

		public AspNetCoreHSMEnabledWithCustomAttributesTests([NotNull] HSMAspNetCoreWebApiCustomAttributesFixture fixture, ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions(
				setupConfiguration: () =>
				{
					var configPath = fixture.DestinationNewRelicConfigFilePath;
					var configModifier = new NewRelicConfigModifier(configPath);
					configModifier.ForceTransactionTraces();

					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] {"configuration", "log"}, "level",
						"debug");
					CommonUtils.ModifyOrCreateXmlAttributeInNewRelicConfig(configPath, new[] {"configuration", "highSecurity"},
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
			var unexpectedTransactionTraceAttributes = new List<String>
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
