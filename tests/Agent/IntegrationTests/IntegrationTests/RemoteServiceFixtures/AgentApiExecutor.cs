using System;
using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class AgentApiExecutor : RemoteApplicationFixture
	{
		private const String ApplicationDirectoryName = @"AgentApiExecutor";
		private const String ExecutableName = @"NewRelic.Agent.IntegrationTests.Applications.AgentApiExecutor.exe";
		private const String TargetFramework = "net35";

		public AgentApiExecutor()
			: base(new RemoteService(ApplicationDirectoryName, ExecutableName, TargetFramework, ApplicationType.Bounded))
		{
		}
	}

	public class HSMAgentApiExecutor : AgentApiExecutor
	{
		public override string TestSettingCategory { get { return "HSM"; } }
	}
}
