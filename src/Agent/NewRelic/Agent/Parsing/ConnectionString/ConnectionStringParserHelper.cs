using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Net;
using JetBrains.Annotations;

namespace NewRelic.Parsing.ConnectionString
{
	public static class ConnectionStringParserHelper
	{
		public static KeyValuePair<String, String>? GetKeyValuePair(DbConnectionStringBuilder connectionStringBuilder, List<String> possibleKeys)
		{
			var key = possibleKeys.FirstOrDefault(k => connectionStringBuilder.ContainsKey(k));
			if (key == null) return null;
			var value = connectionStringBuilder[key].ToString();
			return new KeyValuePair<String, String>(key, value);
		}

		public static String NormalizeHostname([NotNull] String host)
		{
			var localhost = new[] { ".", "localhost" };
			var hostIsLocalhost = localhost.Contains(host);
			if (!hostIsLocalhost)
			{
				IPAddress ipAddress;
				var isIpAddress = IPAddress.TryParse(host, out ipAddress);
				hostIsLocalhost = isIpAddress && IPAddress.IsLoopback(ipAddress);
			}

			var resolvedHostName = hostIsLocalhost ? Dns.GetHostName() : host;
			return resolvedHostName;
		}
	}
}
