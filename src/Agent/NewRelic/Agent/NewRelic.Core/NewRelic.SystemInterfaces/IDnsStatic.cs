using System;
using System.Net;
using JetBrains.Annotations;

namespace NewRelic.SystemInterfaces
{
    public interface IDnsStatic
    {
        [NotNull]
        String GetHostName();

        [NotNull]
        IPHostEntry GetHostEntry([NotNull] String hostNameOrAddres);
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
