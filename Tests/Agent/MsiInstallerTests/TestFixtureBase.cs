using System;
using FunctionalTests.Helpers;
using NUnit.Framework;

namespace FunctionalTests
{
	[TestFixture]
	public abstract class TestFixtureBaseAllOptions
	{
		protected TestServer TServer;

		[OneTimeSetUp]
		public void TestFixtureSetUpBase()
		{
			Common.Log($"TestFixtureBaseAllOptions - Settings.IsDeveloperMode {Settings.IsDeveloperMode}");
			if (!Settings.IsDeveloperMode)
			{
				Console.WriteLine("-- Beginning execution of '{0}'.", this.GetType().Name);
				TServer = new TestServer();
				Common.TestServerContainer.Add(this.GetType().FullName, TServer);
				TServer.CommandLineUninstall(true, testName: nameof(TestFixtureSetUpBase));
				ComponentManager.CleanComponents(TServer, testName: nameof(TestFixtureSetUpBase));
				TServer.EventLog_Clear("Application");
				TServer.CommandLineInstall(licenseKey: Settings.LicenseKey, allFeatures: true, testName: nameof(TestFixtureSetUpBase));
				ComponentManager.TruncateComponents(TServer);
			}
		}

		[OneTimeTearDown]
		public void TestFixtureTearDownBase()
		{
			var server = Common.TestServerContainer[this.GetType().FullName];
			var errors = server.EventLog_CheckForErrors("Application");
			server.ReleaseServerLock();
			var errorMessage = $"One or more unexpected errors were detected in the Application event log:\r\n{String.Join(Environment.NewLine, errors)}";
			Assert.AreEqual(0, errors.Count, errorMessage);
		}
	}
}
