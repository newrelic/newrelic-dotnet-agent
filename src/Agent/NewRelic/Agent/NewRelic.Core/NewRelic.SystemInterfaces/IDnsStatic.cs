using System.Net;

namespace NewRelic.SystemInterfaces
{
    public interface IDnsStatic
    {
        string GetHostName();
        IPHostEntry GetHostEntry(string hostNameOrAddres);
    }

    public class DnsStatic : IDnsStatic
    {
        public string GetHostName()
        {
            return Dns.GetHostName();
        }

        public IPHostEntry GetHostEntry(string hostNameOrAddres)
        {
            return Dns.GetHostEntry(hostNameOrAddres);
        }
    }
}
