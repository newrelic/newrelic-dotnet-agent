using NewRelic.Agent.IntegrationTestHelpers.RemoteServiceFixtures;
using System;
using System.Net;

namespace NewRelic.Agent.IntegrationTests.RemoteServiceFixtures
{
	public class OwinRemotingFixture : RemoteApplicationFixture
	{ 
		private const String ServerApplicationDirectoryName = @"OwinRemotingServer";
		private const String ServerExecutableName = @"OwinRemotingServer.exe";
		private const String ClientApplicationDirectoryName = @"OwinRemotingClient";
		private const String ClientExecutableName = @"OwinRemotingClient.exe";
		internal RemoteService OwinRemotingServerApplication { get; set; }

		public OwinRemotingFixture() : base(new RemoteService(ClientApplicationDirectoryName, ClientExecutableName, ApplicationType.Bounded, createsPidFile: false))
		{
			OwinRemotingServerApplication = new RemoteService(ServerApplicationDirectoryName, ServerExecutableName, ApplicationType.Bounded, createsPidFile: false);
			OwinRemotingServerApplication.CopyToRemote();
			OwinRemotingServerApplication.Start(String.Empty, captureStandardOutput:false, doProfile: false);
		}

		public string GetObjectTcp()
		{
			var address = String.Format(@"http://{0}:{1}/Remote/GetObjectTcp", DestinationServerName, Port);
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
			return result;
		}

		public string GetObjectHttp()
		{
			var address = String.Format(@"http://{0}:{1}/Remote/GetObjectHttp", DestinationServerName, Port);
			var webClient = new WebClient();
			var result = webClient.DownloadString(address);
			return result;
		}

		public override void Dispose()
		{
			OwinRemotingServerApplication.Shutdown();
			OwinRemotingServerApplication.Dispose();
			base.Dispose();
		}
	}
}
