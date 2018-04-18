using System.IO;
using System.Net;
using System.Threading;
using PlatformTests.Applications;

namespace PlatformTests.Fixtures
{
	public class ServiceFabricFixture : BaseFixture
	{
		public ServiceFabricFixture():base(new ServiceFabricApplication("ServiceFabricApplication", new[] { "OwinService"}))
		{
		}

		public void WarmUp()
		{
			var maxTry = 3;
			for (var i = 1; i <= maxTry; i++)
			{
				try
				{
					var address = $@"http://dotnetsftestcluster.westus2.cloudapp.azure.com:8100/api/";
					var request = (HttpWebRequest)WebRequest.Create(address);
					request.Timeout = 180000;
					var response = request.GetResponse();
					break;
				}
				catch
				{
					if (i == maxTry)
						throw;
					
					Thread.Sleep(10000);
				}
			}
		}

		public string GetAgentLog()
		{
			var address = $@"http://dotnetsftestcluster.westus2.cloudapp.azure.com:8100/api/Logs/AgentLog";
			var request = (HttpWebRequest)WebRequest.Create(address);
			request.Timeout = 300000;
			var response = request.GetResponse();
			var stream = new StreamReader(response.GetResponseStream());

			var agentLogString = stream.ReadToEnd();

			agentLogString = agentLogString.TrimStart('"').TrimEnd('"');
			agentLogString = System.Text.RegularExpressions.Regex.Unescape(agentLogString);

			return agentLogString;
		}
	}
}
