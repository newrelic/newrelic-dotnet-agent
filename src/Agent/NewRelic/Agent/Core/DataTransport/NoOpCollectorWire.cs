using System;

namespace NewRelic.Agent.Core.DataTransport
{
	public class NoOpCollectorWire : ICollectorWire
	{
		public String SendData(String method, ConnectionInfo connectionInfo, String serializedData)
		{
			// Any valid JSON without an exception can be returned
			return "{}";
		}
	}
}
