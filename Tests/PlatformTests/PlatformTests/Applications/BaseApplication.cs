using Xunit.Abstractions;

namespace PlatformTests.Applications
{
	public abstract class BaseApplication
	{
		public string ApplicationName { get; }

		public string[] ServiceNames { get; }

		public ITestOutputHelper TestLogger { get; set; }

		protected BaseApplication(string applicationName, string[] serviceNames)
		{
			ApplicationName = applicationName;
			ServiceNames = serviceNames;
		}

		public abstract void InstallAgent();
		public abstract void Build();
		public abstract void BuildAndPackage();
		public abstract void Deploy();
		public abstract void Undeploy();

		public abstract void UpdateNewRelicConfig();
	}
}
