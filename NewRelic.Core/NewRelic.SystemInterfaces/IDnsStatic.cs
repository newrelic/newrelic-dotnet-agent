using System;
using System.Net;

namespace NewRelic.SystemInterfaces
{
	public interface IDnsStatic
	{

		String GetHostName();


		IPHostEntry GetHostEntry(String hostNameOrAddres);
	}

	public class DnsStatic : IDnsStatic
	{
		public String GetHostName()
		{
			return Dns.GetHostName();
		}

		public IPHostEntry GetHostEntry(String hostNameOrAddres)
		{
			return Dns.GetHostEntry(hostNameOrAddres);
		}
	}
}
