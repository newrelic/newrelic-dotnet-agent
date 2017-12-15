using NewRelic.Agent.Configuration;

namespace NewRelic.Agent.Core.DataTransport
{
	public class HttpCollectorWireFactory : ICollectorWireFactory
	{
		public ICollectorWire GetCollectorWire(IConfiguration configuration)
		{
			return new HttpCollectorWire(configuration);
		}
	}
}
