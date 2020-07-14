using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NewRelic.Parsing.ConnectionString
{
	public class StackExchangeRedisConnectionStringParser : IConnectionStringParser
	{
		private readonly String _connectionString;

		public StackExchangeRedisConnectionStringParser(String connectionString)
		{
			_connectionString = connectionString;
		}

		public ConnectionInfo GetConnectionInfo()
		{
			// Other than the hosts, these are MOST of the other possibilities.
			// Not since they all contain and "=" I am filtering by that.
			//var options = new[]
			//{
			//	"abortConnect=","allowAdmin=","channelPrefix=","connectRetry=","connectTimeout=",
			//	"configChannel=","defaultDatabase=","keepAlive=","name=","password=",
			//	"proxy=","resolveDns=","serviceName=","ssl=","sslHost=",
			//	"syncTimeout=","tiebreaker=","version=","writeBuffer="
			//};

			// Example connection string: localhost,resolvedns=1
			// Example connection string: localhost,abortConnect=true
			// Example connection string: localhost,password=awesomesuace, name=stuffandthings

			var sections = _connectionString.Split(',');
			foreach (var section in sections)
			{
				if(section.Contains('=')) continue;

				// We can only capture the first server we detect.  It could be that there are many....
				var hostPortPair = section.Split(':');
				var port = hostPortPair.Length == 2 ? hostPortPair[1] : null;
				return new ConnectionInfo(ConnectionStringParserHelper.NormalizeHostname(hostPortPair[0]), port, null);
			}

			return new ConnectionInfo(null, null, null);
		}
	}
}
