using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NewRelic.Agent.IntegrationTestHelpers;
using NewRelic.Agent.IntegrationTestHelpers.Models;
using NewRelic.Testing.Assertions;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace NewRelic.Agent.IntegrationTests
{
	public class EnvironmentTests : IClassFixture<RemoteServiceFixtures.BasicMvcApplication>
	{
		[NotNull]
		private readonly RemoteServiceFixtures.BasicMvcApplication _fixture;

		public EnvironmentTests([NotNull] RemoteServiceFixtures.BasicMvcApplication fixture, [NotNull] ITestOutputHelper output)
		{
			_fixture = fixture;
			_fixture.TestLogger = output;
			_fixture.Actions
			(
				exerciseApplication: () =>
				{
					_fixture.Get();
				}
			);
			_fixture.Initialize();
		}

		[Fact]
		public void Test()
		{
			var connectData = _fixture.AgentLog.GetConnectData();

			var plugins = connectData?.Environment.GetPluginList();

			Assert.NotEmpty(plugins);

			var hasSystem = plugins.Any(plugin => plugin.Contains("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
			var hasCore = plugins.Any(plugin => plugin.Contains("System.Core, Version=4.0.0.0, Culture=neutral, PublicKeyToken="));
			var hasNrAgentCore = plugins.Any(plugin => plugin.Contains("NewRelic.Agent.Core, Version="));

			NrAssert.Multiple(
				() => Assert.True(hasSystem),
				() => Assert.True(hasCore),
				() => Assert.True(hasNrAgentCore)
			);
		}
	}
}
