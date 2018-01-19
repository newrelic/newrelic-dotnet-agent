using System;

namespace NewRelic.Agent.Extensions.Parsing
{
	public class ConnectionInfo
	{
		public ConnectionInfo(String host, String portPathOrId, String databaseName, String instanceName = null)
		{
			Host = ValueOrUnknown(host);
			PortPathOrId = ValueOrUnknown(portPathOrId);
			DatabaseName = ValueOrUnknown(databaseName);
			InstanceName = instanceName;
		}

		private static string ValueOrUnknown(string value)
		{
			return string.IsNullOrEmpty(value) ? "unknown" : value;
		}

		public String Host { get; private set; }
		public String PortPathOrId { get; private set; }
		public String DatabaseName { get; private set; }
		public String InstanceName { get; private set; }
	}
}
