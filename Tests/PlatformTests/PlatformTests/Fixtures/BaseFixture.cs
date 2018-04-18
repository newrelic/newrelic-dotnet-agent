using System;
using PlatformTests.Applications;
using Xunit.Abstractions;

namespace PlatformTests.Fixtures
{
	public class BaseFixture
	{
		public BaseApplication Application { get; }
		public ITestOutputHelper TestLogger { get; set; }

		public BaseFixture(BaseApplication application)
		{
			Application = application;
		}

		public Action Exercise { get; set; }

		public void Initialize()
		{
			Application.TestLogger = TestLogger;

			Application.InstallAgent();
			Application.BuildAndPackage();
			Application.UpdateNewRelicConfig();
			Application.Deploy();

			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Testing");

			Exercise.Invoke();

			TestLogger?.WriteLine($@"[{DateTime.Now}] ... Tesing done");

			Application.Undeploy();
		}


	}
}
